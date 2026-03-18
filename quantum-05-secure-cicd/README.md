# quantum-05-secure-cicd

Companion code for "Article 05 — Quantum-Safe CI/CD".

## What this does

Extends the standard build pipeline with three quantum-safe additions:

1. **Cosign signing key from QuantumVault** — no plaintext private keys in pipeline variables
2. **Cosign image signing** — cryptographic proof the image came from your pipeline
3. **CycloneDX SBOM generation** — attached to the image for supply chain audits

## Files

| File | Purpose |
|------|---------|
| `azure-pipelines.yml` | 5-stage pipeline: build → scan → image → deploy → smoketest |
| `trivy-config.yaml` | Trivy scanner configuration (CVE + secrets + misconfig) |
| `trivy-secret.yaml` | Custom secret patterns for QuantumVault and QuantumAPI credentials |
| `setup-pipeline-secrets.sh` | One-time setup to generate and store Cosign keypair in QuantumVault |

## Pipeline stages

```
Build ──► Scan ──► Image ──► Deploy ──► Smoketest
          │         │
          │         └── qapi secrets get → Cosign sign → SBOM attach
          └── Trivy CVE scan + CycloneDX SBOM generate
```

## Prerequisites

| Tool | Install |
|------|---------|
| .NET 10+ | [dot.net](https://dot.net) |
| Trivy | `curl -sfL https://raw.githubusercontent.com/aquasecurity/trivy/main/contrib/install.sh \| sh` |
| CycloneDX .NET | `dotnet tool install -g CycloneDX` |
| Cosign | [GitHub Releases](https://github.com/sigstore/cosign/releases) |
| qapi CLI | `brew install quantumapi-eu/tap/qapi` |
| jq | `brew install jq` |

## One-time setup

```bash
# Log in to QuantumAPI
qapi login

# Generate Cosign keypair and store both keys in QuantumVault
chmod +x setup-pipeline-secrets.sh
./setup-pipeline-secrets.sh
```

The script prints the secret IDs for both keys. Copy those into your pipeline variable group.

## Azure DevOps configuration

1. Create a **variable group** named `quantum-api-secrets` with:
   - `QAPI_API_KEY` — your QuantumAPI key (mark as secret)
   - `COSIGN_SECRET_ID` — the ID of the `cosign-signing-key` secret
   - `COSIGN_VERIFY_SECRET_ID` — the ID of the `cosign-verify-key` secret

2. Create a **service connection** named `azure-service-connection` with access to ACR and AKS.

3. Update the variables at the top of `azure-pipelines.yml`:
   - `ACR_NAME` — your Azure Container Registry name
   - `AKS_RESOURCE_GROUP` and `AKS_CLUSTER` — your AKS cluster
   - `K8S_NAMESPACE` — deployment namespace

## Verify a signed image locally

```bash
# Get the public verification key from QuantumVault
qapi secrets get <COSIGN_VERIFY_SECRET_ID> --show --output json \
  | jq -r .value > cosign.pub

# Verify the image signature
cosign verify \
  --key cosign.pub \
  --insecure-ignore-tlog \
  yourregistry.azurecr.io/users-api:abc123

rm -f cosign.pub
```

## Related articles

- [Article 02 — QuantumVault](../quantum-02-quantumvault/) — secrets management used by this pipeline
- [Article 03 — EaaS Encryption](../quantum-03-eaas/) — encryption used in the API being deployed
- [Article 06 — Zero Trust](../quantum-06-zero-trust/) — mTLS setup for services that run in the cluster
