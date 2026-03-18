# Migration guide: custom JWT → QuantumID (OIDC)

This checklist covers everything you need to move the users-api from its
home-grown JWT setup to QuantumID as the OIDC provider. Do the steps in
order — skipping ahead will cause broken deployments.

---

## Phase 1 — Claims inventory (do this before touching any code)

Before you remove anything, understand what claims your app currently uses.
A missed claim in the new token will break authorization silently.

- [ ] List every place in the code that reads a claim
      (`User.FindFirstValue`, `ClaimsPrincipal`, `HttpContext.User`)
- [ ] Record each claim name and its purpose:
  | Claim | Used in | Purpose |
  |-------|---------|---------|
  | `sub` | `GetCallerId()` | User identity / ownership check |
  | `exp` | JWT middleware | Expiry validation |
  | `iss` | JWT middleware | Issuer validation |
- [ ] Map each existing claim to its OIDC equivalent in QuantumID
      (check the QuantumID dashboard → Client → Token settings)
- [ ] Confirm QuantumID will include all required claims in the access token.
      If a claim is only in the ID token, you need to request the right scope
      or enable it in the client config

---

## Phase 2 — QuantumID setup

- [ ] Create an account at [quantumapi.eu](https://quantumapi.eu) if you
      haven't already
- [ ] Create a new client application in the QuantumID dashboard:
  - Application type: **Web application** (Authorization Code + PKCE)
  - Redirect URIs: `https://your-domain.com/signin-oidc`
  - Post-logout redirect URI: `https://your-domain.com/`
  - Silent renew URI: `https://your-domain.com/silent-renew.html`
  - Scopes: `openid profile email` (add more if your app needs them)
- [ ] Note the **Client ID** — you'll need it in appsettings.json and auth.ts
- [ ] Generate a **Client Secret** for the introspection endpoint
      (the frontend uses PKCE only; the secret is only for the back-end
      introspection calls)
- [ ] Test the discovery document:
  ```bash
  curl https://id.quantumapi.eu/.well-known/openid-configuration
  ```
  You should see `introspection_endpoint` listed

---

## Phase 3 — .NET configuration

- [ ] Replace `appsettings.json` content with the version in this folder.
      Update `Audience` and `ClientId` with your actual Client ID
- [ ] Set `ClientSecret` via environment variable — never commit it:
  ```bash
  # In your shell / CI pipeline / Kubernetes Secret
  export OIDC__CLIENTSECRET="your-client-secret"
  ```
  In Kubernetes, mount it from a Secret:
  ```yaml
  env:
    - name: OIDC__CLIENTSECRET
      valueFrom:
        secretKeyRef:
          name: quantumid-credentials
          key: clientSecret
  ```
- [ ] Copy `Program.cs` from this folder, replacing the existing one.
      Key changes: `AddJwtBearer` now uses `Authority` instead of a symmetric
      key. `JwtService` registration is gone
- [ ] Copy `TokenIntrospectionService.cs` into the `Api/` folder
- [ ] Copy `UsersController.cs` into the `Api/` folder.
      The `/auth/login` and `/auth/refresh` endpoints are removed
- [ ] Remove `JwtService.cs` — it is no longer needed
- [ ] Remove the `Jwt:` section from appsettings.json (and all environments)
- [ ] Run the application and hit `/health`:
  ```bash
  dotnet run
  curl http://localhost:5000/health
  ```
- [ ] Confirm the JWKS endpoint resolves on startup (check startup logs for
      any `IDX` or `discovery` errors)

---

## Phase 4 — Endpoint cleanup verification

- [ ] `POST /api/v1/auth/login` returns 404 (removed)
- [ ] `POST /api/v1/auth/refresh` returns 404 (removed — refresh is handled
      by QuantumID's token endpoint)
- [ ] `GET /api/v1/users/{id}` returns 401 without a token
- [ ] `GET /api/v1/users/{id}` returns 200 with a valid QuantumID access token
- [ ] `GET /api/v1/users/{id}` returns 403 when the token subject does not
      match the requested id
- [ ] `DELETE /api/v1/users/{id}` calls introspection and rejects a revoked token

---

## Phase 5 — Frontend integration

- [ ] Install `oidc-client-ts`:
  ```bash
  npm install oidc-client-ts
  ```
- [ ] Copy `auth.ts` from this folder into your frontend project
- [ ] Replace `'your-client-id'` with the actual Client ID
- [ ] Create a `/signin-oidc` route that calls `handleCallback()` on load
- [ ] Create a `/silent-renew.html` file for background token renewal:
  ```html
  <!doctype html>
  <html>
    <head>
      <script type="module">
        import { UserManager } from 'oidc-client-ts';
        new UserManager({}).signinSilentCallback();
      </script>
    </head>
  </html>
  ```
- [ ] Replace all `fetch` calls that used the old login endpoint with calls
      to `getAccessToken()` for the Bearer header
- [ ] Test full login flow: click login → redirect to QuantumID → redirect
      back → token in localStorage → API call succeeds
- [ ] Test logout: session cleared in localStorage and at QuantumID

---

## Phase 6 — Go-live

- [ ] Deploy the updated back-end to staging and repeat Phase 4 verification
- [ ] Deploy the updated front-end to staging and repeat Phase 5 tests
- [ ] Confirm `OIDC__CLIENTSECRET` is set in the production environment and
      **not** in any repository or config file
- [ ] Monitor startup logs for JWKS fetch errors on first deploy
- [ ] Remove any Kubernetes Secrets or environment variables that contained
      the old `JWT__SECRET`
- [ ] Archive or delete the old symmetric key — it is no longer a secret to
      protect, but clean up is good hygiene
