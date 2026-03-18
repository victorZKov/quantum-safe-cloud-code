# quantum-04-quantumid

Code sample for Article 4: replacing the custom JWT setup from `atlas-05-users-api`
with [QuantumID](https://quantumapi.eu) as the OIDC provider.

## What changed

### Removed

| File / thing | Why |
|---|---|
| `Api/JwtService.cs` | The app no longer issues tokens. QuantumID does that. |
| `POST /api/v1/auth/login` | QuantumID owns the login flow. |
| `POST /api/v1/auth/refresh` | QuantumID owns token renewal via its token endpoint. |
| `builder.Services.AddSingleton<JwtService>()` | No more local token issuing. |
| Symmetric key config (`Jwt:Secret`, `Jwt:Issuer`) | Replaced by OIDC authority config. |

### Added

| File | What it does |
|---|---|
| `Program.cs` | Configures `AddJwtBearer` with `Authority` pointing at QuantumID. No symmetric key. Connection pool reuse via `SocketsHttpHandler`. |
| `TokenIntrospectionService.cs` | Calls `/connect/introspect` on QuantumID to check if a token is still active. Used in the delete endpoint to catch revoked tokens before a destructive action. |
| `UsersController.cs` | Ownership check still uses the `sub` claim, but now reads it from a QuantumID-issued token. Login/refresh endpoints removed. |
| `auth.ts` | TypeScript frontend client using `oidc-client-ts`. Handles redirect login, callback, logout, silent renewal, and Access Token retrieval. |
| `appsettings.json` | New `Oidc` section. `ClientSecret` must come from the `OIDC__CLIENTSECRET` environment variable, never from this file. |
| `migration-guide.md` | Step-by-step checklist to move an existing deployment. |

## Key design decisions

**Why `SocketsHttpHandler` with `PooledConnectionLifetime`?**

The JWT middleware fetches the JWKS (signing keys) from QuantumID over HTTP.
Without a pooled handler, .NET creates a new connection on every key refresh.
Setting `PooledConnectionLifetime = 15 minutes` avoids connection exhaustion
on busy services without holding connections open forever.

**Why `RefreshOnIssuerKeyNotFound = true`?**

QuantumID rotates signing keys periodically. If a token arrives signed with a
new key that is not yet in the local cache, the middleware automatically fetches
the updated JWKS instead of rejecting the token. This means you get zero-downtime
key rotation for free.

**Why `ClockSkew = 30s` and not `TimeSpan.Zero`?**

In a distributed system — multiple pods, Azure Load Balancer in front, QuantumID
servers in a different data centre — clocks are never perfectly in sync.
30 seconds is a safe margin that prevents valid tokens from being rejected due
to a few seconds of drift.

**Why is introspection only in the delete endpoint?**

JWT validation (signature + expiry) happens on every request. Introspection
adds a network call, so it is only used where the cost of accepting a revoked
token is highest: irreversible operations. For read endpoints the JWT expiry
window (max 30 seconds of extra lifetime) is an acceptable trade-off.

## Configuration

```json
{
  "Oidc": {
    "Authority": "https://id.quantumapi.eu",
    "Audience": "your-client-id",
    "ClientId": "your-client-id",
    "ClientSecret": ""
  }
}
```

Set `ClientSecret` via environment variable:

```bash
export OIDC__CLIENTSECRET="your-client-secret"
```

In Kubernetes:

```yaml
env:
  - name: OIDC__CLIENTSECRET
    valueFrom:
      secretKeyRef:
        name: quantumid-credentials
        key: clientSecret
```

## Files in this folder

```
Program.cs                  Updated startup — OIDC auth, no JwtService
TokenIntrospectionService.cs  Revocation check via /connect/introspect
UsersController.cs          Updated controller — login endpoints removed
auth.ts                     Frontend OIDC client (oidc-client-ts)
appsettings.json            Config template — replace client ID values
migration-guide.md          Step-by-step checklist for migrating a live app
README.md                   This file
```
