# auth-reference-dotnet

A .NET 8 identity provider + resource server built layer-by-layer following Clean Architecture. Each layer ships as its own commit — read the git history to see the shape of the refactor. Target end-state: standards-compliant OAuth 2.0 / OIDC (via OpenIddict) with production-shaped operational scaffolding — token-version revocation, refresh-token rotation with reuse detection, structured logging, real health checks, named per-endpoint rate limits, background retention.

**Status: Phase 1 of 5 — Domain layer only.**

## Phases

| Phase | Ships | Status |
| --- | --- | --- |
| 1 — Domain | Entities, value objects, service interfaces, domain events. Zero framework deps. | done |
| 2 — Application | MediatR CQRS handlers over the domain interfaces. Unit-testable with fakes. | pending |
| 3 — Infrastructure | Postgres + EF Core migration, OpenIddict configuration + seed, service implementations. | pending |
| 4 — Server (IdP) | Minimal-API endpoints, correlation-id middleware, named rate limits, payload caps, Redis token-version store, retention worker. | pending |
| 5 — Resource API + tests + Docker + README | Token-version enforcement on the resource server, handler + integration tests, docker-compose with Postgres + Redis, expanded README. | pending |

See [`docs/comparison-and-refactor.md`](docs/comparison-and-refactor.md) for the full architectural rationale — why Clean Architecture, what patterns the design borrows from a production auth service, and what it deliberately does *not* borrow.

## What Phase 1 gives you

A `AuthReference.Domain` project that compiles standalone and contains everything the rest of the service will depend on:

- **Entities** — `ApplicationUser` (with the `TokenVersion` linchpin), `RefreshToken` (with rotation chain), `AuthAuditEvent`.
- **Models** — request / response records for login, register, refresh, revoke-all, change-password. Plus a `TokenPair` and an explicit `AuthDecision` enum.
- **Service interfaces** — `IPasswordAuthenticator`, `ITokenIssuer`, `IRefreshTokenStore`, `ITokenVersionStore`, `IUserRegistrar`, `IUserLookup`, `IPasswordChanger`, `IClock`. Every dependency the Application layer will need, defined by contract.
- **Domain events** — `UserRegisteredEvent`, `PasswordChangedEvent`, `RefreshTokenReuseDetectedEvent`, `AllTokensRevokedEvent`.

The project has **no NuGet references**. That's a deliberate constraint that keeps framework concerns out of the domain and forces every side-effecting operation to go through an interface.

## Build

```bash
dotnet restore AuthReference.sln
dotnet build AuthReference.sln
```

Should succeed with zero warnings.

## License

MIT — see [LICENSE](LICENSE).
