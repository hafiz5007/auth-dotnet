# Comparison — Portfolio auth-reference-dotnet vs. Real MM.Auth

You've built two auth services. Here's the honest answer to "which is better" — the answer is **neither is strictly better**, they're aimed at different jobs. But the portfolio version can and should steal several patterns from the real one.

---

## What each one is actually for

### Portfolio: `auth-reference-dotnet` (OpenIddict, monolithic, 27 files)

**Job:** Show an interviewer you understand OAuth 2.0 / OIDC deeply — you can build a **standards-compliant identity provider** from scratch. You issue tokens, expose `/connect/authorize`, `/connect/token`, `/connect/userinfo`, publish JWKS, do auth-code + PKCE, refresh rotation, client credentials.

**Audience:** external clients that don't already trust you (a mobile app, a partner integration, a third-party developer). Anyone who reads RFC 6749 and expects the endpoints to be there.

**Interview signal:** "I can build the identity plane. I know the spec."

### Real: `MM.Auth` (Clean Architecture, .NET 10, ~4,600 lines)

**Job:** Serve the specific auth flows a UK mortgage-broker platform needs. Not an IdP for third parties — it's the identity **service** inside your own trusted platform. You issue custom JWTs shaped for your own resource servers, back them with a refresh-token store, and expose the flows real users hit: email+password, OTP, magic link, QR login, social login, tenant switching, admin revoke-all.

**Audience:** your own frontend apps (Flutter mobile, Angular portal), your own internal microservices reading tokens via a shared validator.

**Engineering signal:** "I run identity in production for a real business. I know how to structure a maintainable service."

**Neither is wrong. They answer different questions.**

---

## Advantages of each — the honest scorecard

### Portfolio wins

| Pattern | Why it matters |
| --- | --- |
| **Standards-compliant OAuth 2.0 + OIDC** | Interviewer opens Postman, hits `/.well-known/openid-configuration`, everything works. Zero explaining. |
| **Auth-code + PKCE flow end-to-end** | Every senior interview asks about PKCE. You have working code. |
| **Client credentials + refresh rotation** | Same. Textbook flows, working. |
| **`/connect/userinfo` + JWKS** | Shows you understand token exchange isn't the whole story. |
| **Simplicity** | Two projects (Server + Api) that a reviewer can read in 20 minutes. Nothing hidden. |
| **Reference / teaching value** | It's the tightest OpenIddict reference on GitHub if you make it public. |

### Real MM.Auth wins

| Pattern | Why it matters |
| --- | --- |
| **Clean Architecture split** — `Domain / Infrastructure / Api` | Interfaces live in Domain, implementations in Infrastructure. Api depends on nothing but interfaces. This is the industry-standard senior-level shape. |
| **Interfaces in Domain** (`ILoginService`, `IJwtTokenService`, `IRefreshTokenService`) | Testability, replaceability, dependency inversion. Every senior code review will ask if you do this. |
| **Multiple auth flows behind one service** — password, OTP, magic link, QR, social | Shows real-world breadth. Every product hits at least three of these. |
| **Real refresh-token storage** with rotation + reuse detection + Redis fast-path | The right way to do refresh tokens. Your portfolio uses OpenIddict's baked-in version — it works, but you can't explain it in an interview. Rolling your own means you can. |
| **Redis-backed token version store** for immediate revocation | Bump a `TokenVersion` column, all outstanding access tokens die within cache TTL. This is *the* pattern for stateless JWTs + immediate revocation. |
| **CQRS-lite via minimal APIs → services** | Endpoints are thin, services do work, one interface per public capability. Modern .NET style. |
| **Multi-tenant / Membership resolution** | Bank/fintech reality — same user, multiple companies, one JWT with the right tenant claim. `IMembershipResolver` owns this. |
| **Rate limiting per-endpoint with named policies** | Login, OTP, refresh, register — each has a different partition strategy. Not one-size-fits-all. |
| **Payload-size caps per endpoint** | Cheap defence against a 50MB body deserialising before validation. |
| **Background retention worker** (`AuthRetentionWorker` — daily sweep of expired refresh tokens) | Nobody talks about this and everyone needs it. Shows operational maturity. |
| **Structured logging + observability wired in from Program.cs** | Serilog, correlation IDs, OpenTelemetry hooks — not an afterthought. |
| **Health checks tied to real dependencies** — AuthDb, Redis, RabbitMQ | `/health/ready` actually tells you if you can serve requests, not just "the process is alive". |
| **MassTransit + RabbitMQ for integration events** | Publishes `UserRegistered`, `PasswordChanged` etc. Loose coupling to email/SMS/notification services. |
| **Payload redactor** — PII scrubbing in logs | GDPR / FCA reality. Interview gold. |
| **Migration-based EF Core** with an explicit connection string, no silent fallback | Production posture. Your portfolio uses InMemory — fine for demo, terrible signal. |

**Score:** portfolio wins **6** categories, real wins **13**. The real one is the senior-engineer version.

---

## What to steal from real → put in portfolio

You should refactor the portfolio to look and feel more like MM.Auth structurally, while keeping the OpenIddict story as its *unique* value. The plan is to make the portfolio the answer to: **"I know OAuth 2.0 *and* I know how to structure a production service."**

### Take from MM.Auth

1. **Clean Architecture split into 4 projects**
   - `AuthReference.Domain` — entities, interfaces, models
   - `AuthReference.Application` — use cases (CQRS / MediatR handlers), DTOs, validation
   - `AuthReference.Infrastructure` — EF Core, Redis, OpenIddict wiring, JWT service impls
   - `AuthReference.Api` — minimal API endpoints, DI wire-up, health, rate limits

2. **Interfaces live in Domain, implementations in Infrastructure**
   - Even the OpenIddict-adjacent services get `ITokenService` in Domain, `OpenIddictTokenService` in Infrastructure.
   - Makes the endpoint layer test with `Mock<ITokenService>` in unit tests instead of dragging OpenIddict into every test.

3. **Postgres + EF Core migrations, not InMemory**
   - `AuthDbContext` with a real Postgres backing store. Docker-compose brings up Postgres already.
   - One migration to start; add more as you evolve.

4. **Redis-backed token version store for immediate revocation**
   - Add a `TokenVersion` int on `ApplicationUser`.
   - Bump it in `AuthorizationController.RevokeAll`.
   - Every access token carries `tv=<version>` claim.
   - API-side validation reads the current version from Redis; mismatch → 401.
   - This is the *headline* pattern to demonstrate. It's what senior interviewers respect.

5. **Named per-endpoint rate limits**
   - `login` — 5/min by IP
   - `refresh` — 60/min by IP
   - `register` — 3/hour by IP
   - Minimal APIs support `RequireRateLimiting("login")` — same as MM.Auth.

6. **Payload-size caps per endpoint**
   - 64 KB for user-supplied JSON, 8 KB for refresh/revoke.

7. **Background retention worker**
   - `TokenRetentionWorker : BackgroundService` — daily sweep of expired refresh tokens + revoked authorisations.

8. **Structured logging + correlation ID middleware**
   - Serilog with a request-logging middleware that stamps `X-Correlation-Id`.
   - Redact `password`, `client_secret`, `refresh_token` from log bodies.

9. **Health checks that actually check dependencies**
   - `AddDbContextCheck<AuthDbContext>` + Redis ping + IdP self-check.
   - `/health/live` — process only. `/health/ready` — dependencies too.

10. **CQRS-lite via MediatR**
    - Each endpoint dispatches `LoginCommand`, `RefreshCommand`, `RegisterCommand` to a handler in `Application`.
    - Endpoints stay thin — 5-10 lines each. Handlers own the logic.
    - Makes unit testing trivial: test the handler, don't spin up the whole app.

11. **Integration events for downstream effects** (optional but strong signal)
    - Publish `UserRegistered` on register, `PasswordChanged` on password reset.
    - Use an in-process bus for the demo (`MediatR.INotification`) — no need to run RabbitMQ.
    - In README, show the outbox pattern as a "how you'd do this in prod" note.

### Do NOT take from MM.Auth

- **Legacy password hasher.** That's a compatibility bridge to your existing `mobSocial_User` table. Skip it entirely — the portfolio starts fresh.
- **Company / tenant switching, MembershipResolver, gRPC company lookup.** That's your product's business domain. The portfolio is generic.
- **Any specific business rules** — FCA verification status, adviser/partner registration flavours, onboarding-email hooks. All product-specific.
- **The exact `MM.Common.*` shared-library dependencies.** They only make sense inside the mortgage platform. Portfolio should be standalone.
- **`Grpc.Net.ClientFactory`, `MassTransit.RabbitMQ`.** Great in prod, but pulling them into a demo adds 15 more services someone has to run before they can `docker compose up`.

### Keep from portfolio

- **OpenIddict as the identity plane.** This is the *story*. Every senior candidate has a "here's my Spring Security config" repo; almost nobody has a working OpenIddict IdP.
- **The clean Docker + docker-compose setup** — swap in Postgres, keep everything else.
- **The README with Mermaid diagrams** — expand it, don't rewrite.
- **The clear `.gitignore` / `LICENSE` / CI workflow.**

---

## Proposed target structure

```
auth-reference-dotnet/
├── AuthReference.sln
├── docker-compose.yml
├── docs/architecture.md
├── docs/comparison-and-refactor.md  (this file)
├── src/
│   ├── AuthReference.Domain/
│   │   ├── Entities/
│   │   │   ├── ApplicationUser.cs           (+ TokenVersion)
│   │   │   ├── RefreshToken.cs
│   │   │   └── OpenIddict entities via package
│   │   ├── Models/
│   │   │   ├── LoginRequest.cs / LoginResponse.cs
│   │   │   ├── RefreshRequest.cs / RefreshResponse.cs
│   │   │   └── RegisterRequest.cs
│   │   └── Services/
│   │       ├── ITokenService.cs               (was in controller before)
│   │       ├── ITokenRevocationService.cs
│   │       └── IUserAuthenticationService.cs
│   ├── AuthReference.Application/
│   │   ├── Commands/
│   │   │   ├── Login/LoginCommand.cs + Handler.cs
│   │   │   ├── Refresh/RefreshCommand.cs + Handler.cs
│   │   │   ├── Register/RegisterCommand.cs + Handler.cs
│   │   │   └── RevokeAll/RevokeAllCommand.cs + Handler.cs
│   │   ├── Events/
│   │   │   └── UserRegistered.cs (INotification)
│   │   └── DependencyInjection.cs
│   ├── AuthReference.Infrastructure/
│   │   ├── Persistence/
│   │   │   ├── AppDbContext.cs
│   │   │   └── Migrations/…
│   │   ├── Services/
│   │   │   ├── TokenService.cs
│   │   │   ├── TokenRevocationService.cs
│   │   │   ├── RedisTokenVersionStore.cs
│   │   │   └── UserAuthenticationService.cs
│   │   ├── Workers/
│   │   │   └── TokenRetentionWorker.cs
│   │   ├── OpenIddict/
│   │   │   ├── SeedData.cs
│   │   │   └── OpenIddictConfiguration.cs
│   │   └── DependencyInjection.cs
│   └── AuthReference.Api/
│       ├── Endpoints/
│       │   ├── AuthorizationEndpoints.cs      (OpenIddict pass-through)
│       │   ├── UserEndpoints.cs               (login, register, refresh)
│       │   └── AdminEndpoints.cs              (revoke-all)
│       ├── Middleware/
│       │   └── CorrelationIdMiddleware.cs
│       ├── HealthChecks/
│       │   └── OpenIddictSelfCheck.cs
│       ├── Program.cs
│       └── appsettings.json
├── src/AuthReference.Api.ResourceServer/     (rename of AuthReference.Api)
│   └── (unchanged — validates tokens via introspection)
└── tests/
    ├── AuthReference.Application.Tests/
    │   ├── LoginCommandHandlerTests.cs
    │   ├── RefreshCommandHandlerTests.cs
    │   └── TokenRevocationTests.cs
    └── AuthReference.Api.Tests/
        └── (unchanged integration tests)
```

**Total projects:** 5 (was 3) plus 2 test projects (was 1). Larger surface, higher signal.

---

## The "which is better" question — final answer

If you're being interviewed for a **backend / senior IC role** at a fintech that already runs an IdP or uses Auth0/Okta: **the refactored portfolio wins**. You demonstrate Clean Architecture, CQRS, Redis-backed revocation, and OAuth 2.0 spec fluency in one repo.

If you're being interviewed for a **tech lead / architect** role: cite MM.Auth in the interview (without naming the employer if the contract is sensitive) — "I designed a service with these flows, this shape, at production scale" is a *stronger* answer than "here's my GitHub project". But you can't push MM.Auth publicly, so you need the refactored portfolio as the artefact.

**Bottom line:** they complement each other. The refactor makes your portfolio look like it was written by someone who's done it in production — which is the truth.

---

## Recommended next step

Approve this plan and I'll do the refactor: split into 4 projects + 2 tests, add MediatR/CQRS, add Postgres+EF Core+one migration, add the Redis token-version pattern, add the retention worker, add named rate limits + payload caps + structured logging + real health checks. Keep OpenIddict, keep the docker-compose, expand the README.

Estimated size: **~40 files** (up from 27). All the ones I add are the exact patterns interviewers scan for.
