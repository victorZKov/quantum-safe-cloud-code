# quantum-06-zero-trust

Companion code for "Article 06 — Zero Trust with Quantum-Safe mTLS".

## What this does

Adds ML-DSA-65 mutual TLS to service-to-service communication using certificates
issued by the QuantumAPI CA. Every connection — inbound and outbound — requires
a valid certificate from the same CA. No certificate = connection rejected.

Combined with Kubernetes NetworkPolicy, this gives two layers of protection:
- **L3/L4**: NetworkPolicy blocks unknown pods at the packet level
- **L7**: mTLS rejects connections without a valid QuantumAPI CA certificate

## Project structure

```
src/ZeroTrust/
├── Certificates/
│   ├── IServiceCertificateProvider.cs      # Interface: get client cert + CA cert
│   ├── QuantumApiCertificateProvider.cs    # Fetches ML-DSA-65 certs from QuantumAPI CA
│   └── CertificateRotationService.cs       # BackgroundService: proactive renewal
├── Http/
│   └── MtlsHttpClientFactory.cs           # AddMtlsHttpClient() extension method
├── Program.cs                             # Kestrel mTLS config + DI wiring
└── appsettings.json
k8s/
├── deployment.yaml                        # Pod spec with security context
└── network-policy.yaml                    # Ingress/egress rules
```

## How certificate rotation works

```
Startup
  │
  ▼
CertificateRotationService starts
  │
  ▼
QuantumApiCertificateProvider.GetClientCertificateAsync()
  │  POST /v1/pki/issue → QuantumAPI CA
  │  ← ML-DSA-65 cert (24h TTL)
  │
  ▼
Cache cert. Mark expiry at 80% of lifetime (≈ 19.2h)
  │
  ▼
Every 6 hours: check if past threshold
  │  If yes → issue new cert
  │  If no  → use cached cert
  ▼
All connections use the active cert transparently
```

## QuantumAPI CA endpoints used

| Endpoint | Purpose |
|----------|---------|
| `POST /v1/pki/issue` | Issue a new ML-DSA-65 client certificate |
| `GET /v1/pki/ca/pem` | Download the CA certificate (for chain validation) |

## Running locally

```bash
# Set your QuantumAPI credentials
export QuantumApi__BaseUrl=https://api.quantumapi.eu
export QuantumApi__ApiKey=qapi_...
export QuantumApi__ServiceName=users-api-local

cd src/ZeroTrust
dotnet run
```

The service will fetch a certificate from QuantumAPI CA on startup and listen
on `https://localhost:443` with mTLS required.

## Kubernetes deployment

```bash
# Create the secret with your QuantumAPI key
kubectl create secret generic quantumapi-credentials \
  --namespace production \
  --from-literal=api-key=qapi_...

# Apply deployment and network policy
kubectl apply -f k8s/deployment.yaml
kubectl apply -f k8s/network-policy.yaml
```

## Testing mTLS locally

```bash
# Request a test certificate from QuantumAPI
curl -X POST https://api.quantumapi.eu/v1/pki/issue \
  -H "Authorization: Bearer qapi_..." \
  -H "Content-Type: application/json" \
  -d '{"common_name":"test-client","ttl":"1h","key_type":"ml-dsa-65"}' \
  | jq -r '.data.certificate_pem' > test-client.crt
  | jq -r '.data.private_key_pem' > test-client.key

# Call the service with the client cert
curl --cert test-client.crt --key test-client.key \
  https://localhost:443/health
```

## Related articles

- [Article 02 — QuantumVault](../quantum-02-quantumvault/) — the API key for the CA comes from QuantumVault
- [Article 05 — Secure CI/CD](../quantum-05-secure-cicd/) — images deployed here are signed by that pipeline
- [Article 07 — Reference Architecture](../quantum-07-reference/) — where this fits in the full picture
