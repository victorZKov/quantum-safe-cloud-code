# quantum-02-quantumvault

This is the `users-api` from `atlas-05` extended to use [QuantumVault](https://quantumapi.eu) for secrets management. The database connection string and JWT signing secret are no longer stored in `appsettings.json`. They live in QuantumVault and are fetched at startup.

The only thing you need to provide to the running service is the QuantumVault API key, via the environment variable `QUANTUMAPI__APIKEY`.

## What changed from atlas-05

| atlas-05 | quantum-02 |
|---|---|
| `ConnectionStrings:DefaultConnection` in appsettings.json | Secret ID in `Secrets:DbConnectionId`, value fetched from vault |
| `Jwt:Secret` in appsettings.json | Secret ID in `Secrets:JwtSecretId`, value fetched from vault |
| `JwtService` reads from `IConfiguration` | `JwtService` reads from `JwtOptions` (singleton, resolved at startup) |
| No secret abstraction | `ISecretProvider` / `QuantumVaultSecretProvider` |

## Project structure

```
quantum-02-quantumvault/
├── Api/
│   ├── JwtOptions.cs                  # Record holding JWT config resolved at startup
│   ├── JwtService.cs                  # Generates tokens from JwtOptions
│   └── UsersController.cs             # Unchanged from atlas-05
├── Application/                       # Unchanged from atlas-05
├── Domain/                            # Unchanged from atlas-05
├── Infrastructure/
│   ├── ISecretProvider.cs             # Abstraction over the vault client
│   ├── QuantumVaultSecretProvider.cs  # Calls QuantumAPI.Client
│   ├── QuantumVaultDbContextFactory.cs # Resolves connection string at startup
│   ├── UsersDbContext.cs              # Unchanged from atlas-05
│   └── UserRepository.cs             # Unchanged from atlas-05
├── Program.cs                         # Updated registration and startup secret fetch
├── appsettings.json                   # Secret IDs only, no secret values
├── UsersApi.csproj                    # Adds QuantumAPI.Client package
├── deploy-secrets.sh                  # Creates secrets in QuantumVault via curl
└── migration-checklist.md             # Step-by-step migration guide
```

## Quick start

### 1. Create your secrets in QuantumVault

```bash
export QUANTUMAPI_KEY="qid_your_api_key_here"
export DB_CONNECTION="Host=your-db-host;Database=users;Username=app;Password=strong-password"
export JWT_SECRET="your-minimum-32-char-signing-secret-here"

./deploy-secrets.sh
```

The script prints the secret IDs. Copy them into `appsettings.json`:

```json
{
  "Secrets": {
    "DbConnectionId": "paste-the-id-printed-by-deploy-secrets.sh",
    "JwtSecretId":    "paste-the-id-printed-by-deploy-secrets.sh"
  }
}
```

### 2. Run the service

```bash
export QUANTUMAPI__APIKEY="qid_your_api_key_here"
dotnet run
```

The double underscore in `QUANTUMAPI__APIKEY` maps to `QuantumApi:ApiKey` in the .NET configuration hierarchy.

### 3. Kubernetes

Create a secret for the API key:

```bash
kubectl create secret generic quantumvault-credentials \
  --from-literal=QUANTUMAPI__APIKEY=qid_your_api_key_here
```

Mount it in your Deployment as an env var — not a ConfigMap, not a volume file.

## Dependencies

- [QuantumAPI.Client](https://nuget.org/packages/QuantumAPI.Client) NuGet package
- Everything else is the same as atlas-05 (EF Core, Npgsql, Argon2id, Swashbuckle, JWT Bearer)
