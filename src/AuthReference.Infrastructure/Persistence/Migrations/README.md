# Migrations

This folder is deliberately empty. The initial migration is generated locally
on your machine so EF Core's design-time services produce a matching
`AppDbContextModelSnapshot.cs` — a hand-forged snapshot is fragile and blocks
future migrations from applying cleanly.

## Generate the initial migration

The Server host (added in Phase 4) is the startup project EF needs to build
the DI graph. **Until Phase 4 lands, run this against any tiny host** — see
the fallback below.

Once Phase 4 exists:

```bash
dotnet tool install --global dotnet-ef            # one-time, if not installed
dotnet ef migrations add InitialCreate \
    --project src/AuthReference.Infrastructure \
    --startup-project src/AuthReference.Server \
    --output-dir Persistence/Migrations
```

This will produce three files here:

- `20260701nnnn_InitialCreate.cs`
- `20260701nnnn_InitialCreate.Designer.cs`
- `AppDbContextModelSnapshot.cs`

Commit all three.

## Apply the migration to a running Postgres

```bash
dotnet ef database update \
    --project src/AuthReference.Infrastructure \
    --startup-project src/AuthReference.Server
```

The connection string comes from the Server host's `appsettings.json`:
`AuthReference:Database:ConnectionString`.

## Before Phase 4 exists

If you want to generate the migration now to unblock local testing before the
Server host lands, add a temporary console startup project or point `dotnet
ef` at any other minimal `Microsoft.NET.Sdk.Web` project that references
`AuthReference.Infrastructure`. It only needs to compile — EF Core will build
the model from `AppDbContext` without ever running the app.

## What the initial migration will contain

- `users` table with a unique index on `email`, an optimistic-concurrency
  `token_version` column, and a comma-separated `roles` column.
- `refresh_tokens` table with unique index on `token_hash`, non-unique index on
  `user_id`, and a non-unique index on `expires_at_utc` for the retention worker.
- `auth_audit_events` table with non-unique indexes on `user_id` and
  `occurred_at_utc`.
- OpenIddict tables (`OpenIddictApplications`, `OpenIddictAuthorizations`,
  `OpenIddictScopes`, `OpenIddictTokens`) picked up automatically because
  `options.UseOpenIddict()` is in `DependencyInjection.cs`.
