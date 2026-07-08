# auth-reference-dotnet

A .NET 10 identity provider + resource server built layer-by-layer following Clean Architecture. Each layer ships as its own commit — read the git history to see the shape of the refactor. Target end-state: standards-compliant OAuth 2.0 / OIDC (via OpenIddict) with production-shaped operational scaffolding — token-version revocation, refresh-token rotation with reuse detection, structured logging, real health checks, named per-endpoint rate limits, background retention.

**Status: Phase 4 of 5 — Server host + operational hardening.**

## Phases

| Phase | Ships | Status |
| --- | --- | --- |
| 1 — Domain | Entities, value objects, service interfaces, domain events. Zero framework deps. | done |
| 2 — Application | MediatR CQRS command handlers over the domain interfaces, FluentValidation pipeline, unit tests with in-memory fakes. | done |
| 3 — Infrastructure | Postgres + EF Core, OpenIddict core + client seeder, PBKDF2 password hashing, JWT issuance, refresh-token rotation with reuse detection. Migration guidance for local generation. | done |
| 4 — Server (IdP) | REPR-style minimal-API endpoints, correlation-id middleware + Serilog PII redaction, named rate limits, payload caps, Redis-backed token-version store with JWT `tv`-claim enforcement, daily retention worker. | done |
| 5 — Resource API + tests + Docker + README | Standalone resource-server sample, integration tests, docker-compose with Postgres + Redis, expanded README + architecture diagram. | pending |

See [`docs/comparison-and-refactor.md`](docs/comparison-and-refactor.md) for the full architectural rationale — why Clean Architecture, what patterns the design borrows from a production auth service, and what it deliberately does *not* borrow.

## What Phase 1 gives you

A `AuthReference.Domain` project that compiles standalone and contains everything the rest of the service will depend on:

- **Entities** — `ApplicationUser` (with the `TokenVersion` linchpin), `RefreshToken` (with rotation chain), `AuthAuditEvent`.
- **Models** — request / response records for login, register, refresh, revoke-all, change-password. Plus a `TokenPair` and an explicit `AuthDecision` enum.
- **Service interfaces** — `IPasswordAuthenticator`, `ITokenIssuer`, `IRefreshTokenStore`, `ITokenVersionStore`, `IUserRegistrar`, `IUserLookup`, `IUserActivityRecorder`, `IPasswordChanger`, `IClock`. Every dependency the Application layer will need, defined by contract.
- **Domain events** — `UserRegisteredEvent`, `PasswordChangedEvent`, `RefreshTokenReuseDetectedEvent`, `AllTokensRevokedEvent`.
- **Cryptography helper** — `RefreshTokenHasher` (SHA256, BCL only). Ensures raw refresh tokens never land in a store.

The project has **no NuGet references**. That's a deliberate constraint that keeps framework concerns out of the domain and forces every side-effecting operation to go through an interface.

## What Phase 2 gives you

`AuthReference.Application` — the use cases, expressed as MediatR command handlers over the Domain interfaces.

- **Five commands** — `LoginCommand`, `RegisterCommand`, `RefreshCommand`, `RevokeAllCommand`, `ChangePasswordCommand`. Each has a record type, a handler, and (where inputs need it) a FluentValidation validator.
- **`ValidationBehavior`** — MediatR pipeline that runs every registered validator before the handler sees the request. Throws `ValidationException` on failure; the API layer will map that to 400.
- **Domain-event notifications** — thin `INotification` wrappers around `UserRegisteredEvent`, `PasswordChangedEvent`, `RefreshTokenReuseDetectedEvent`, `AllTokensRevokedEvent`. Handlers publish these through MediatR so downstream consumers can react without touching the auth handlers.
- **Refresh-token rotation with reuse detection** — the RefreshCommandHandler is the substantive piece. When a token that has already been replaced is presented again, we treat the chain as compromised, revoke every session for the user, and publish a security event. This is the pattern to talk about in interviews.
- **`AuthReference.Application.Tests`** — 13 tests across five handlers + the validation pipeline. Uses hand-rolled in-memory fakes (`FakeUserLookup`, `FakeRefreshTokenStore`, `FakeClock`, `FakeTokenIssuer`, `CapturingPublisher` etc). Zero infrastructure needed to run: `dotnet test` from anywhere.

The Application layer's only new dependencies are **MediatR 12** and **FluentValidation 11**. Everything else is Domain interfaces.

## What Phase 3 gives you

`AuthReference.Infrastructure` — everything the Application layer needs to actually run against a real database.

- **`AppDbContext`** — EF Core 10 against Postgres via Npgsql 10, with `UseOpenIddict()` so OpenIddict's entity sets sit inside the same context.
- **Three `IEntityTypeConfiguration` classes** — one per domain entity. Explicit column names + indexes; `token_version` marked as concurrency token so a losing racer doesn't clobber a bump.
- **Postgres refresh-token store** with `ExecuteUpdateAsync` optimistic rotation — the "match on `replaced_by_id IS NULL`" trick means concurrent refreshes decide a single winner without an explicit transaction.
- **PBKDF2 password hasher** (600k iterations, current OWASP guideline) with the `NeedsRehash` upgrade path — when iteration counts are raised in configuration, hashes upgrade silently on next successful login.
- **JWT token issuer** — HS256 for now, easy swap to RS256 in production. Every access token carries a `tv` claim equal to the user's `TokenVersion` at issue time; that's the pin for immediate revocation.
- **In-memory token-version store** as a Phase-3 placeholder; Phase 4 replaces it with a Redis-backed implementation that survives restarts and shares state across replicas.
- **OpenIddict client seeder** — creates an SPA client (auth-code + PKCE, no secret) and a backend-worker client (client-credentials) idempotently on boot.
- **`AddAuthReferenceInfrastructure()`** — one DI call the Server host will make in Phase 4.

Migrations are generated locally (see `src/AuthReference.Infrastructure/Persistence/Migrations/README.md`) so EF Core's design-time services produce a matching `AppDbContextModelSnapshot`. Committing a hand-forged snapshot is fragile and would block future migrations.

## What Phase 4 gives you

`AuthReference.Server` — the runnable ASP.NET Core 10 host.

- **REPR-style minimal-API endpoints** — one file per verb+path. `POST /api/auth/login`, `/register`, `/refresh`, `/change-password`, `/admin/revoke-all`. Endpoints are 10-line adapters that construct a `MediatR` command and shape the response. All the logic lives in the Application handlers, so integration tests don't need a live server to exercise it.
- **Correlation IDs everywhere.** `CorrelationIdMiddleware` extracts or mints `X-Correlation-Id`, echoes it back on the response, and pushes it into the Serilog log context so every log line for a request is tagged with it.
- **PII log redaction.** A Serilog enricher scrubs `password`, `refresh_token`, `client_secret`, `access_token` values and long-shaped JWTs out of every log event's string properties. Belt-and-braces on top of the primary defence of not logging secrets to begin with.
- **Named per-endpoint rate limits** — `auth-login` (5/min), `auth-register` (3/hr), `auth-refresh` (60/min). Partitioned by client IP. Same shape as MM.Auth's `auth-login` policy.
- **Payload-size caps** — 2-16 KB per endpoint. A 50 MB POST body will never deserialise before validation runs.
- **Token-version enforcement on every validated JWT.** `JwtBearerEvents.OnTokenValidated` reads the `tv` claim off the presented token, compares it against the value in `ITokenVersionStore`, and fails the request if the token is stale. The cache is cold-repopulated from Postgres on first miss.
- **Redis-backed `TokenVersionStore`.** Registered automatically when `AuthReference:Redis:ConnectionString` is set; a short (60s) TTL keeps stale reads bounded. Password change and admin revoke-all invalidate the key so the next check reads the fresh version.
- **Background retention worker** — daily sweep that hard-deletes refresh tokens past the retention grace and audit rows past the retention window. Configurable via `AuthReference:Retention`.
- **Real health checks** — `/health/live` (process only) and `/health/ready` (Postgres via `AddDbContextCheck<AppDbContext>` + Redis ping when configured). K8s can wire these directly.

**JWT signing.** `JwtTokenIssuer` uses HS256 with a symmetric key from `AuthReference:Jwt:SigningKey`. The bearer-validation middleware reads the same key so tokens issued and validated by this host stay consistent. Production would flip to RS256 with the private key in Key Vault and JWKS publishing.

**Distinct from MM.Auth** — same architectural pattern and same operational primitives, but with a different stack (Postgres over SQL Server, minimal APIs over broader MVC, StackExchange.Redis directly instead of via a shared common package, no RabbitMQ/gRPC dependencies). The Phase 4 hardening — rate limits, payload caps, correlation IDs, PII redaction, real health checks — mirrors production patterns from MM.Auth without copying the domain-specific pieces (Membership, company gRPC lookup, legacy password hasher).

## Build

```bash
dotnet restore AuthReference.sln
dotnet build AuthReference.sln
```

Should succeed with zero warnings.

## License

MIT — see [LICENSE](LICENSE).
