# quantum-03-eaas

Code for article 03 of the QuantumAPI series. This extends the `atlas-05-users-api` project to encrypt PII fields — `Email` and `PhoneNumber` — using [QuantumAPI](https://quantumapi.io) Encryption as a Service (EaaS) before writing them to PostgreSQL.

## What changes from the original users-api

| Before | After |
|--------|-------|
| `Email` column (plaintext) | `EmailCiphertext` column (EaaS ciphertext) + `SearchableEmail` column (SHA3-256 hash) |
| No phone storage | `PhoneNumberCiphertext` column (nullable, EaaS ciphertext) |
| `UserRepository` depends on `UsersDbContext` only | `UserRepository` also depends on `IEncryptionService` |
| Email lookup via `WHERE Email = @email` | Email lookup via `WHERE SearchableEmail = SHA3-256(@email)` |

## Files

| File | Purpose |
|------|---------|
| `IEncryptionService.cs` | Interface: `EncryptAsync` / `DecryptAsync` |
| `QuantumApiEncryptionService.cs` | Implementation using `QuantumApiClient` |
| `User.cs` | Updated entity — PII fields replaced with ciphertext columns |
| `UserRepository.cs` | Updated repository — encrypts on write, decrypts on read, hashes for lookup |
| `UsersMigration.cs` | EF Core migration: adds new columns, drops old `Email` column |
| `encrypt-existing-data.sh` | One-time script to backfill existing plaintext rows before enforcing NOT NULL |
| `Program.cs` | DI registration snippet for `QuantumApiClient` and `IEncryptionService` |

## How it works

### Write path

```
CreateUserDto.Email
    │
    ├─ EncryptAsync(email)        → EmailCiphertext   (stored in DB)
    ├─ HashEmail(email)           → SearchableEmail   (stored in DB, used for lookups)
    └─ EncryptAsync(phoneNumber)  → PhoneNumberCiphertext (stored in DB, nullable)
```

Encryption and phone encryption run in parallel via `Task.WhenAll`.

### Read path

```
DB row (ciphertexts)
    │
    ├─ DecryptAsync(EmailCiphertext)        → Email
    └─ DecryptAsync(PhoneNumberCiphertext)  → PhoneNumber (if present)
```

Both decrypt calls run in parallel via `Task.WhenAll`.

### Login lookup

```
login request: email = "user@example.com"
    │
    └─ HashEmail("user@example.com") → WHERE SearchableEmail = 'a3f9...'
```

No decryption needed to find the user. The hash is deterministic and collision-resistant (SHA3-256).

## Migration steps

Run these in order — do not skip step 2.

```bash
# 1. Apply the migration (adds nullable columns, does NOT drop Email yet)
dotnet ef database update AddPiiEncryption

# 2. Backfill existing rows
QUANTUMAPI_KEY=qid_xxx \
DATABASE_URL="postgres://user:pass@host:5432/usersdb" \
./encrypt-existing-data.sh

# 3. The migration's second ALTER TABLE block enforces NOT NULL and drops Email.
#    If you split this into two migrations, apply the second one now.
```

If you have no existing data (fresh database), skip step 2 entirely.

## Configuration

Add to `appsettings.json` (or use environment variables / Azure Key Vault in production):

```json
{
  "QuantumApi": {
    "ApiKey": "qid_your_key_here",
    "BaseUrl": "https://api.quantumapi.io/v1"
  }
}
```

In Kubernetes, inject `QuantumApi__ApiKey` as a secret-backed environment variable — never commit the key to source control.

## NuGet package

Add `QuantumAPI.Client` to the project:

```bash
dotnet add package QuantumAPI.Client
```

## Security notes

- `SearchableEmail` is a one-way hash. You cannot recover the email from it. It is only useful for exact-match lookups.
- `EmailCiphertext` and `PhoneNumberCiphertext` are decrypted only at the application layer, never in SQL queries.
- The QuantumAPI key must be rotated via key management — re-encryption of existing rows is required on key rotation. That is a separate operational concern not covered here.
- Soft-deleted users keep their ciphertexts. If you need right-to-erasure (GDPR Article 17), overwrite the ciphertexts with a sentinel value on deletion rather than using `IsDeleted`.
