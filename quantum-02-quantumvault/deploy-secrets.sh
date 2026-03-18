#!/usr/bin/env bash
# Creates the required secrets in QuantumVault for the users-api.
#
# Usage:
#   DB_CONNECTION="Host=your-host;Database=users;Username=app;Password=strong-password" \
#   JWT_SECRET="your-minimum-32-char-signing-secret" \
#   ./deploy-secrets.sh
#
# Requires: qapi CLI (brew install quantumapi-eu/tap/qapi) + qapi login or QAPI_API_KEY set
#
# After running, copy the printed secret IDs into appsettings.json under
# Secrets:DbConnectionId and Secrets:JwtSecretId.

set -euo pipefail

# ---------------------------------------------------------------------------
# Validate required environment variables
# ---------------------------------------------------------------------------
if [[ -z "${DB_CONNECTION:-}" ]]; then
  echo "Error: DB_CONNECTION is not set." >&2
  echo "Export the full PostgreSQL connection string before running this script." >&2
  exit 1
fi

if [[ -z "${JWT_SECRET:-}" ]]; then
  echo "Error: JWT_SECRET is not set." >&2
  echo "Export a random string of at least 32 characters before running this script." >&2
  exit 1
fi

if ! command -v qapi &>/dev/null; then
  echo "Error: qapi CLI not found." >&2
  echo "Install it: brew install quantumapi-eu/tap/qapi" >&2
  exit 1
fi

# Verify connectivity
qapi health --no-color >/dev/null 2>&1 || {
  echo "Error: cannot reach QuantumAPI. Run 'qapi health' for details." >&2
  exit 1
}

# ---------------------------------------------------------------------------
# Create secrets
# ---------------------------------------------------------------------------
echo ""
echo "Creating secret: users-api-db-connection ..."
qapi secrets create users-api-db-connection "$DB_CONNECTION" --output json

echo ""
echo "Creating secret: users-api-jwt-secret ..."
qapi secrets create users-api-jwt-secret "$JWT_SECRET" --output json

# ---------------------------------------------------------------------------
# Print the IDs for copy/paste into appsettings.json
# ---------------------------------------------------------------------------
echo ""
echo "Secrets created. Get the IDs with:"
echo "  qapi secrets list"
echo ""
echo "Then add to appsettings.json:"
echo '  "Secrets": {'
echo '    "DbConnectionId": "<id from list above>",'
echo '    "JwtSecretId":    "<id from list above>"'
echo '  }'
echo ""
