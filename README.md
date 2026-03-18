# Quantum-Safe Cloud — Code Examples

Reference implementations for the [Quantum-Safe Cloud article series](https://victorz.cloud) on victorz.cloud.

Each folder is a self-contained, runnable project for one article. The code is
production-quality — not toy examples. You can clone any folder and run it.

---

## Folders

| Folder | Article | What it builds |
|--------|---------|----------------|
| [quantum-02-quantumvault](./quantum-02-quantumvault/) | Article 02 | QuantumVault secret resolution in .NET — bootstrapping before DI |
| [quantum-03-eaas](./quantum-03-eaas/) | Article 03 | Field-level PII encryption: ML-KEM-768 + AES-256-GCM, SHA3-256 search hash |
| [quantum-04-quantumid](./quantum-04-quantumid/) | Article 04 | QuantumID OIDC integration — ML-DSA JWT validation, token introspection |
| [quantum-05-secure-cicd](./quantum-05-secure-cicd/) | Article 05 | Quantum-safe CI/CD: qapi CLI secrets, Cosign ML-DSA signing, CycloneDX SBOM |
| [quantum-06-zero-trust](./quantum-06-zero-trust/) | Article 06 | mTLS with QuantumAPI CA (ML-DSA-65), Kubernetes NetworkPolicy |
| [quantum-07-reference](./quantum-07-reference/) | Article 07 | Full ATLAS document + GOTCHA prompt + production checklist |

---

## Prerequisites

| Tool | Version | Notes |
|------|---------|-------|
| [.NET](https://dot.net) | 10.0+ | Runtime + SDK |
| [Docker](https://www.docker.com/) | any recent | For building container images |
| [kubectl](https://kubernetes.io/docs/tasks/tools/) | 1.29+ | For Kubernetes manifests |
| [qapi CLI](https://quantumapi.eu) | latest | `curl -sSL https://releases.quantumapi.eu/qapi/latest/install.sh \| bash` |
| [Cosign](https://github.com/sigstore/cosign) | 2.4.1+ | For image signing verification |

## QuantumAPI account

Most examples require a [QuantumAPI](https://quantumapi.eu) account.
Get your API key at the QuantumAPI dashboard after registration.

Each folder's README lists exactly which services it uses (QuantumVault, EaaS, QuantumID, CA).

---

## Stack

- **Runtime**: .NET 10, C# 13
- **Database**: PostgreSQL
- **Container**: Docker (multi-stage builds, non-root user)
- **Orchestration**: Kubernetes (AKS)
- **CI/CD**: Azure DevOps
- **PQC standards**: ML-KEM (FIPS 203), ML-DSA (FIPS 204)

---

## License

MIT — use freely, attribution appreciated but not required.
