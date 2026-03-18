# Migration checklist: moving secrets to QuantumVault

Use this checklist when you migrate an existing .NET service from appsettings.json secrets to QuantumVault. Go through it in order. Every item must be done before you deploy to production.

---

## 1. Audit your current secrets

- [ ] List every value in appsettings.json that is sensitive (connection strings, signing keys, API keys, passwords).
- [ ] Check environment-specific files (appsettings.Development.json, appsettings.Production.json) for secrets.
- [ ] Check docker-compose.yml and k8s ConfigMaps — secrets must not live there either.
- [ ] Check CI/CD pipeline variables. Identify which ones are real secrets vs. plain config.

---

## 2. Create secrets in QuantumVault

- [ ] Install the QuantumVault CLI or use the `deploy-secrets.sh` script in this repo.
- [ ] Run `deploy-secrets.sh` with your production values. Keep the output (secret IDs) safe.
- [ ] Verify each secret was created: `curl https://api.quantumapi.eu/api/v1/secrets -H "Authorization: Bearer $QUANTUMAPI_KEY"`.
- [ ] Set a meaningful name for each secret (`service-name/secret-purpose`). This makes auditing easier.
- [ ] Add a description to every secret. Future-you will thank you.

---

## 3. Update appsettings.json

- [ ] Replace every secret value with an empty string or remove the key entirely.
- [ ] Add a `Secrets` section with the QuantumVault secret IDs (not the values).
- [ ] Add a `QuantumApi` section with an empty `ApiKey` field — the value comes from env, not config.
- [ ] Commit the updated appsettings.json. It is now safe to commit because it contains no secret values.

---

## 4. Update the application code

- [ ] Add the `QuantumAPI.Client` NuGet package.
- [ ] Add `ISecretProvider` and `QuantumVaultSecretProvider` (see this repo).
- [ ] Register `AddQuantumApiClient` in Program.cs, reading the API key from `QUANTUMAPI__APIKEY`.
- [ ] Register `AddScoped<ISecretProvider, QuantumVaultSecretProvider>`.
- [ ] Replace direct reads of `builder.Configuration.GetConnectionString(...)` and `Configuration["Jwt:Secret"]` with calls to `ISecretProvider.GetSecretAsync(secretId)`.
- [ ] Remove any class that still reads secrets directly from `IConfiguration`.

---

## 5. Update infrastructure

- [ ] **Kubernetes**: create a Secret object for `QUANTUMAPI__APIKEY`. Do not store it in the ConfigMap.
  ```yaml
  kubectl create secret generic quantumvault-credentials \
    --from-literal=QUANTUMAPI__APIKEY=qid_xxx
  ```
- [ ] Mount the Kubernetes secret as an environment variable in the Deployment spec (not as a volume file).
- [ ] **Docker Compose (local dev)**: use an `.env` file for `QUANTUMAPI__APIKEY`. Add `.env` to `.gitignore`.
- [ ] **CI/CD pipeline**: store `QUANTUMAPI__APIKEY` as a pipeline secret variable, not a plain variable.
- [ ] Remove any previously hard-coded connection strings or JWT secrets from ConfigMaps, pipeline YAML, or Dockerfiles.

---

## 6. Test before deploying

- [ ] Run the service locally with the real QuantumVault API key in your shell environment.
- [ ] Hit `/health` and confirm it returns 200 (database connection works with the vault-fetched string).
- [ ] Call `POST /api/v1/users` and `POST /api/v1/auth/login` end-to-end.
- [ ] Verify a valid JWT is accepted on an authenticated endpoint.
- [ ] Intentionally set a wrong API key and confirm the service fails to start with a clear error.
- [ ] Intentionally set a wrong secret ID and confirm the service fails to start with a clear error.

---

## 7. Rotate the old secrets

- [ ] Change the database password. Update the secret value in QuantumVault.
- [ ] Change the JWT signing secret. Update the secret value in QuantumVault. Note: existing tokens become invalid after rotation — plan for that if you have long-lived tokens.
- [ ] If the old secrets were in git history, rotate them even if you have removed them from the codebase.
- [ ] Revoke any old API keys that were stored in plain text.

---

## 8. Clean up

- [ ] Run `git log --all -S "your-old-secret"` to check git history for leaked secrets.
- [ ] If found in history, use `git filter-repo` to remove them and force-push. Notify your team.
- [ ] Remove old `.env` files from any shared drives or wikis.
- [ ] Verify no secrets appear in application logs (search for password, connection string fragments).

---

## 9. Document

- [ ] Update your service README with: how to get a QuantumVault API key, how to run `deploy-secrets.sh` for a new environment.
- [ ] Note the secret IDs (not values) in your team's internal docs so the next person knows what exists in the vault.

---

## Done

If every box above is checked, your service no longer stores secrets in config files, environment-specific JSON, or source control. The only sensitive value that needs protecting is the QuantumVault API key itself, and that lives as a Kubernetes secret or a CI pipeline secret variable.
