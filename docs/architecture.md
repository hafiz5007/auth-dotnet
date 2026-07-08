# Architecture notes

## Layer responsibilities

**Domain** — the framework-free heart of the service. Contains entities, value objects, service *interfaces*, and domain-event *records*. No NuGet references. Cannot know about EF Core, HTTP, OpenIddict, or MediatR. Its only job is to describe what an auth service *is*, not how it does anything.

**Application** — the use cases, expressed as MediatR command records + handlers over the Domain interfaces. Depends only on Domain. Ships a FluentValidation pipeline behavior that runs on every request before the handler sees it. Publishes MediatR `INotification` wrappers around the Domain events so subscribers stay decoupled. This layer is the only place where "what the service does" is spelled out; every side effect goes through an interface, so 15 unit tests using in-memory fakes cover the whole set of user-facing behaviors.

**Infrastructure** — the plumbing. EF Core against Postgres, OpenIddict client + scope management, Redis-backed `TokenVersionStore` (with an in-memory fallback for single-node dev / tests), JWT bearer configuration, JwtTokenIssuer that signs the access tokens the whole system trusts, PBKDF2 password hasher, background retention worker. Two DI entry points — `AddAuthReferenceInfrastructure` for the IdP and `AddAuthReferenceValidation` for resource servers — let the two hosts share plumbing without dragging the IdP's write-side into the read-only Api.

**Server** — the ASP.NET Core 10 IdP host. Composes Application + Infrastructure, wires JWT bearer with the shared `tv`-claim enforcement hook, maps REPR-style minimal-API endpoints, attaches middleware (correlation IDs, PII redaction, validation-exception → 400), configures named rate-limit policies, exposes health checks.

**Api** — the ASP.NET Core 10 resource-server host. Composes only the validation slice of Infrastructure — no MediatR, no password hasher, no token issuer, no retention worker. Just enough to validate tokens issued by the Server against the shared signing key + shared TokenVersion cache.

## Key architectural choices

### One DbContext, shared by IdP and Api

Both hosts talk to the same Postgres database via the same `AppDbContext`. That's a deliberate choice for the demo — it lets a resource server do the cold-cache `TokenVersion` fallback without inventing an introspection protocol. In a real fintech deployment there are three legitimate designs:

1. **Shared DbContext, shared DB** (what this repo does). Simplest. Fine when resource servers and the IdP are in the same trust domain and deploy together.
2. **Separate DBs, introspection endpoint** — resource servers call `/introspect` on the IdP for cache misses. Adds a hop, decouples deployments, works across trust boundaries.
3. **Separate DBs, JWKS + no revocation** — resource servers only validate signature + expiry. Immediate revocation is impossible; you rely on short access-token lifetimes. Simplest to deploy at scale.

The Server + Api split here demonstrates the **shape** of design 2 (two hosts, no shared code beyond Domain contracts) with the **wiring** of design 1 (shared DbContext). A production migration to design 2 is a `TokenVersionCheck` rewrite that hits `/introspect` instead of Postgres.

### `tv` claim over token blacklist

Two ways to revoke a JWT that isn't yet expired:

1. **Blacklist** — write the jti to a "revoked" set. Every resource server consults the set on every request. Set grows unboundedly; requires per-jti tracking.
2. **Token version** (this repo). Bump `user.TokenVersion` in one write; every access token issued before that write becomes stale. No per-jti storage. Trade-off: revocation is per-user, not per-session — you can't revoke *one* session while keeping the others alive.

For the "an ex-employee's account must be dead within a minute" case, `tv` is the right tool. For "sign me out on this one device", you rotate the refresh token (or clear it client-side) — the access token expires naturally in 10 minutes.

### Refresh-token rotation with reuse detection

`PostgresRefreshTokenStore.TryRotateAsync` uses `ExecuteUpdateAsync` with a `Where(t => t.Id == presented.Id && t.ReplacedById == null)` predicate. Concurrent refreshes decide a single winner: the first update lands, the second matches zero rows and returns false. The handler then treats a zero-row response as reuse, revokes every session for the user, and publishes `RefreshTokenReuseDetectedEvent`.

`FindActiveByHashAsync` deliberately returns revoked-but-not-yet-pruned rows so the reuse-detection path can inspect `ReplacedById`. Callers that only want valid tokens must check `IsActive`. Documented in the store's XML comments.

### Two DI entry points in Infrastructure

`AddAuthReferenceInfrastructure` (full stack) and `AddAuthReferenceValidation` (lighter, for resource servers). Both share a private `AddAuthReferenceCore` — options binding, DbContext, TokenVersionStore, clock, HeadlessRequestContext. The full stack adds Password authenticator + Token issuer + OpenIddict Core + retention worker; the validation-only stack skips those. Api uses the lighter one; Server uses the full one.

### JWT signing shared via `Infrastructure.Authentication.JwtBearerConfiguration`

This lives in Infrastructure — one file both hosts consume — so a signing-key or issuer change happens in exactly one place. Pulling `Microsoft.AspNetCore.Authentication.JwtBearer` into Infrastructure gently violates "Infrastructure has no ASP.NET Core dependencies", but the alternative is a duplicated 60-line class in every resource server. Pragmatism wins here.

### Migrations generated locally, not hand-crafted

`Persistence/Migrations/README.md` documents the `dotnet ef migrations add InitialCreate` command. Hand-forged migrations desync from the model snapshot on the next migration, so I don't ship a hand-written one. Server is the startup project EF uses; the migration lands in Infrastructure alongside the DbContext.

## What Phase 4 hardening looks like in isolation

- `CorrelationIdMiddleware` reads or mints `X-Correlation-Id`, echoes it on the response, and pushes into Serilog `LogContext` so every log line for a request is tagged. Log correlation in a distributed trace is the difference between a 10-minute incident and a 3-hour one.
- `PiiRedactor` scrubs `password`, `refresh_token`, `client_secret`, `access_token`, and JWT-shaped strings out of every log property. Not a substitute for not-logging-secrets, but a robust backstop when a body ends up in an unstructured log.
- `AuthRateLimitPolicies` — anonymous surfaces are IP-partitioned with different windows per endpoint. Fixed-window is fine for demo scale; production would consider sliding-window for smoother-ratio enforcement.
- Payload caps via `RequestSizeLimitMetadata` on each endpoint. 2 KB for refresh, 8 KB for login/change-password, 16 KB for register. A 50 MB body never hits `System.Text.Json`.
- `AddDbContextCheck<AppDbContext>` gives `/health/ready` a real signal — the process is only ready when it can round-trip a `SELECT 1` against Postgres. Same for Redis when configured.

## What's deliberately not here

- **No RabbitMQ / MassTransit.** In-process MediatR notifications cover the demo's event-publishing needs. A real product would emit `UserRegistered` etc. through an outbox → RabbitMQ pattern for downstream email / analytics / CRM consumers.
- **No multi-tenancy.** No Membership resolver, no per-tenant claim, no tenant switching. If your day job needs this, model it as a Domain entity plus an interface implemented in Infrastructure.
- **No SPA login UI.** OpenIddict is wired at Core level, so the `/connect/authorize` / `/connect/token` OpenIddict Server pipeline needs a login page. Adding it is a Razor Page in Server + a `SignInManager` call — mechanical work, out of scope for the reference.
- **No mTLS / DPoP.** Bearer tokens only. Both are appropriate upgrades for a production fintech deployment.
- **No key rotation.** Signing key is a single symmetric key from config. Production uses RS256 with rotating keys published via JWKS.
