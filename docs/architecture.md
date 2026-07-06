# Architecture notes

## Why OpenIddict

The two mainstream .NET options for hosting an OAuth 2.0 / OIDC identity provider are OpenIddict and Duende IdentityServer. Both implement the same specs. Duende ships more built-in UI and administration, but requires a commercial licence beyond a very small revenue cap. OpenIddict is Apache-2 licensed, standards-compliant, and actively maintained. For a portfolio demo — and for many real production systems that don't need Duende's paid features — OpenIddict is the right choice.

## Separation of concerns

Server and API are separate processes with separate lifecycles. That mirrors reality: the identity provider is stable, deployed rarely, backed by a hardened key store; the API deploys many times a day. They only communicate through the OAuth spec — no shared code, no shared database, no runtime coupling.

The API validates tokens by **introspection** (calling the IdP), not by loading JWKS and validating signatures locally. Both are legal; the trade-off is:

- **Local JWKS** — sub-millisecond, no runtime dependency on the IdP, but token revocation only takes effect at next refresh.
- **Introspection** — adds one HTTP round-trip per request, but revocation is immediate and the IdP can decide which resource each token is valid for.

For a fintech API where "an ex-employee's session must be dead within a minute" matters more than the round-trip latency, introspection wins. In production it's cached (with a very short TTL) via a distributed cache. OpenIddict's validation handler supports this out of the box; the demo has caching off for clarity.

## Claim destinations

By default OpenIddict does not put every claim into every token — you decide. `AuthorizationController.GetDestinations` enumerates claim → destination:

- `sub`, `role`, and app-specific claims → access token
- `name`, `email`, `preferred_username` → access token, plus id token if the corresponding scope was granted
- `AspNet.Identity.SecurityStamp` → never

The rule of thumb: **anything that would embarrass you if it appeared in a Sentry log should not go in the access token**. Access tokens end up in server logs, load balancer traces, and browser dev tools. Id tokens stay with the client.

## Refresh token rotation + reuse detection

Every refresh returns a new refresh token AND invalidates the old one. If the old token is presented after being replaced, OpenIddict revokes the whole authorisation grant on the assumption that someone has stolen the refresh token from the legitimate client.

This is not free — the IdP needs to remember every issued refresh token until it expires. In a stateless JWT-only setup that's not possible. Which is why we're using OpenIddict's server-side token store rather than a pure JWT-only setup.

## PKCE

Required for all public clients. The `spa-client` registration includes:

```csharp
Requirements = { Requirements.Features.ProofKeyForCodeExchange }
```

Which makes OpenIddict reject any auth code request that doesn't send a `code_challenge`. This blocks the classic auth-code interception attack against SPAs and native apps.

## What's missing vs. production

- **Real login UI.** The demo's `AuthorizationController` auto-signs the seeded user. A real IdP has a Razor Page (or Blazor / Next.js frontend) that collects username + password, verifies with Identity, and posts back to `/connect/authorize`.
- **MFA.** Once the login UI is there, adding TOTP or WebAuthn via `Microsoft.AspNetCore.Identity` is a small step.
- **Persistent store.** The demo uses `UseInMemoryDatabase`. Swap for Postgres via `UseNpgsql` and add a migration.
- **Key rotation.** `AddDevelopmentSigningCertificate` generates ephemeral keys. Production loads a rotating key from Key Vault or similar. OpenIddict supports overlapping key windows so rotation is zero-downtime.
- **Rate limiting.** `/connect/token` and `/connect/authorize` need aggressive rate limits by client id and IP.
- **Consent.** The demo uses `ConsentTypes.Implicit`. Any client an end-user hasn't explicitly onboarded should use `Explicit` and route through a consent page.
