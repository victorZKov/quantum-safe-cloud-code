#!/usr/bin/env bash
# setup-pipeline-secrets.sh
#
# One-time setup: generate Cosign keypair and store both keys in QuantumVault as secrets.
# The private key is encrypted at rest inside QuantumVault (ML-KEM-768).
# The pipeline retrieves it by secret ID at sign time — no plaintext secrets in variables.
#
# Prerequisites:
#   - cosign CLI installed (https://github.com/sigstore/cosign/releases)
#   - qapi CLI installed: brew install quantumapi-eu/tap/qapi
#   - Logged in: qapi login (or QAPI_API_KEY set in environment)
#   - jq installed: brew install jq

set -euo pipefail

for tool in cosign qapi jq; do
  if ! command -v "$tool" &>/dev/null; then
    echo "Error: $tool not found." >&2
    exit 1
  fi
done

echo "==> Checking QuantumAPI connectivity..."
qapi health --no-color

echo ""
echo "==> Generating Cosign keypair..."
# COSIGN_PASSWORD="" = no passphrase (QuantumVault handles encryption at rest)
COSIGN_PASSWORD="" cosign generate-key-pair

echo ""
echo "==> Storing private key in QuantumVault..."
SIGNING_ID=$(qapi secrets create cosign-signing-key "$(cat cosign.key)" --output json | jq -r .id)
echo "    Secret ID: $SIGNING_ID"

echo ""
echo "==> Storing public key in QuantumVault..."
VERIFY_ID=$(qapi secrets create cosign-verify-key "$(cat cosign.pub)" --output json | jq -r .id)
echo "    Secret ID: $VERIFY_ID"

echo ""
echo "==> Cleaning up local key files..."
rm -f cosign.key cosign.pub

echo ""
echo "==> Done. Add these to Azure DevOps variable group 'quantum-api-secrets':"
echo ""
echo "    QAPI_API_KEY             = <your QuantumAPI key>  (mark as secret)"
echo "    COSIGN_SECRET_ID         = $SIGNING_ID"
echo "    COSIGN_VERIFY_SECRET_ID  = $VERIFY_ID"
echo ""
echo "Confirm secrets exist:"
echo "    qapi secrets list"
