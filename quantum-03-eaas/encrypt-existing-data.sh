#!/usr/bin/env bash
# One-time migration script to encrypt existing plaintext emails in the database.
# Reads rows with a NULL EmailCiphertext, encrypts via QuantumAPI EaaS, then writes
# the ciphertext and SearchableEmail hash back to the same row.
#
# Run this AFTER applying the AddPiiEncryption migration (columns exist but are nullable),
# and BEFORE the second migration step enforces NOT NULL.
#
# Usage:
#   QUANTUMAPI_KEY=qid_xxx \
#   DATABASE_URL="postgres://user:pass@host:5432/usersdb" \
#   ./encrypt-existing-data.sh
#
# Dependencies: psql, curl, jq
# Tested on: bash 5+, psql 15+

set -euo pipefail

: "${QUANTUMAPI_KEY:?QUANTUMAPI_KEY environment variable is required}"
: "${DATABASE_URL:?DATABASE_URL environment variable is required}"

QUANTUMAPI_BASE_URL="${QUANTUMAPI_BASE_URL:-https://api.quantumapi.io/v1}"
BATCH_SIZE="${BATCH_SIZE:-100}"

log()  { printf '[%s] %s\n' "$(date -u +%H:%M:%S)" "$*"; }
die()  { log "ERROR: $*" >&2; exit 1; }

command -v psql  &>/dev/null || die "psql is not installed"
command -v curl  &>/dev/null || die "curl is not installed"
command -v jq    &>/dev/null || die "jq is not installed"

# ---------------------------------------------------------------------------
# encrypt_value: calls QuantumAPI EaaS and returns the EncryptedPayload string
# ---------------------------------------------------------------------------
encrypt_value() {
    local plaintext="$1"

    local response
    response=$(curl --silent --fail --show-error \
        --request POST \
        --url "${QUANTUMAPI_BASE_URL}/encryption/encrypt" \
        --header "Authorization: Bearer ${QUANTUMAPI_KEY}" \
        --header "Content-Type: application/json" \
        --data "$(jq -n --arg p "$plaintext" '{Plaintext: $p, Encoding: "utf8"}')")

    jq -r '.EncryptedPayload' <<< "$response"
}

# ---------------------------------------------------------------------------
# sha3_256_hex: SHA3-256 of the lowercased, trimmed input — matches HashEmail()
# in UserRepository.cs. Requires OpenSSL 3+ for SHA3-256 support.
# ---------------------------------------------------------------------------
sha3_256_hex() {
    local value
    value=$(printf '%s' "$1" | tr '[:upper:]' '[:upper:]' | xargs | tr '[:upper:]' '[:lower:]')
    printf '%s' "$value" | openssl dgst -sha3-256 -hex | awk '{print $2}'
}

# ---------------------------------------------------------------------------
# Main loop: fetch rows in batches, encrypt each one, write back
# ---------------------------------------------------------------------------
log "Starting PII encryption for existing users..."
log "QuantumAPI base URL: ${QUANTUMAPI_BASE_URL}"
log "Batch size: ${BATCH_SIZE}"

total_processed=0
total_failed=0

while true; do
    # Fetch a batch of rows that still have a NULL EmailCiphertext
    mapfile -t rows < <(psql "$DATABASE_URL" --tuples-only --no-align --field-separator='|' \
        --command "SELECT \"Id\", \"Email\" FROM \"Users\"
                   WHERE \"EmailCiphertext\" IS NULL
                     AND \"IsDeleted\" = false
                   ORDER BY \"CreatedAt\"
                   LIMIT ${BATCH_SIZE}")

    if [[ ${#rows[@]} -eq 0 ]]; then
        log "No more rows to process."
        break
    fi

    log "Processing batch of ${#rows[@]} rows..."

    for row in "${rows[@]}"; do
        user_id="${row%%|*}"
        plain_email="${row##*|}"

        # Trim whitespace that psql might add
        user_id="${user_id// /}"
        plain_email="${plain_email// /}"

        if [[ -z "$user_id" || -z "$plain_email" ]]; then
            log "WARN: skipping malformed row: '${row}'"
            (( total_failed++ )) || true
            continue
        fi

        # Encrypt and compute hash
        encrypted_email=$(encrypt_value "$plain_email") || {
            log "WARN: encryption failed for user ${user_id}, skipping"
            (( total_failed++ )) || true
            continue
        }

        search_hash=$(sha3_256_hex "$plain_email")

        # Write back both values atomically
        psql "$DATABASE_URL" --quiet \
            --command "UPDATE \"Users\"
                       SET \"EmailCiphertext\" = $(printf '%s' "$encrypted_email" | psql "$DATABASE_URL" -At -c "SELECT quote_literal('${encrypted_email//\'/\'\'}')"),
                           \"SearchableEmail\"  = '${search_hash}'
                       WHERE \"Id\" = '${user_id}'"

        (( total_processed++ )) || true
        log "  encrypted user ${user_id} (${total_processed} done so far)"
    done
done

log "Done. Processed: ${total_processed}, Failed: ${total_failed}"

if [[ $total_failed -gt 0 ]]; then
    log "WARNING: ${total_failed} rows were not encrypted. Re-run the script to retry."
    exit 1
fi
