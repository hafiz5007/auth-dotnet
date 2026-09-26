using AuthReference.Domain.Entities;
using AuthReference.Domain.Services;
using AuthReference.Infrastructure.Services;
using AuthReference.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using StackExchange.Redis;

namespace AuthReference.Api.Tests.TestHarness;

/// <summary>
/// Boots the Api host in-process with two swaps for test friendliness:
///  * DbContext is redirected to an EF Core InMemory provider (no Postgres running).
///  * The Redis-backed TokenVersionStore is replaced with the in-memory one
///    (no Redis running). appsettings.json points at localhost:6379, and a null
///    config override does not unset it, so the swap happens in DI.
///
/// A seeded user is added on startup so tests can mint valid tokens against a
/// stable known subject id.
/// </summary>
public class ApiFactory : WebApplicationFactory<Program>
{
    public const string TestSigningKey = "test-only-32-byte-signing-key-!!!";
    public static readonly Guid AliceId = Guid.Parse("11111111-2222-3333-4444-555555555555");
    public static readonly Guid BobId   = Guid.Parse("22222222-3333-4444-5555-666666666666");
    // Reserved for the stale-token-version test, which bumps this user's TokenVersion.
    public static readonly Guid CarolId = Guid.Parse("33333333-4444-5555-6666-777777777777");

    // One database per factory — each test class gets its own fixture instance.
    private readonly string _databaseName = $"api-tests-{Guid.NewGuid()}";

    protected override IHost CreateHost(IHostBuilder builder)
    {
        builder.ConfigureAppConfiguration(cfg =>
        {
            cfg.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AuthReference:Database:ConnectionString"] = "Host=localhost;Database=test;Username=x;Password=x",
                ["AuthReference:Jwt:SigningKey"] = TestSigningKey,
                ["AuthReference:Jwt:Issuer"] = "https://test-issuer.local/",
                ["AuthReference:Jwt:Audience"] = "auth-reference-api"
            }!);
        });

        builder.ConfigureServices(services =>
        {
            // Swap AppDbContext to InMemory. The Postgres registration is removed
            // and a fresh InMemory one is added under the same DI key. Since EF Core 9
            // the provider config lives in IDbContextOptionsConfiguration<T>, so that
            // must go too or both providers end up registered.
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<AppDbContext>>();

            services.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase(_databaseName));

            services.RemoveAll<IConnectionMultiplexer>();
            services.RemoveAll<ITokenVersionStore>();
            services.AddSingleton<ITokenVersionStore, InMemoryTokenVersionStore>();

            // Seed users.
            var sp = services.BuildServiceProvider();
            using var scope = sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Database.EnsureCreated();
            if (!db.Users.Any(u => u.Id == AliceId))
            {
                db.Users.Add(new ApplicationUser
                {
                    Id = AliceId,
                    Email = "alice@example.com",
                    PasswordHash = "test:x",
                    DisplayName = "Alice",
                    Roles = "user",
                    TokenVersion = 1
                });
            }
            if (!db.Users.Any(u => u.Id == BobId))
            {
                db.Users.Add(new ApplicationUser
                {
                    Id = BobId,
                    Email = "bob@example.com",
                    PasswordHash = "test:x",
                    DisplayName = "Bob",
                    Roles = "user,admin",
                    TokenVersion = 1
                });
            }
            if (!db.Users.Any(u => u.Id == CarolId))
            {
                db.Users.Add(new ApplicationUser
                {
                    Id = CarolId,
                    Email = "carol@example.com",
                    PasswordHash = "test:x",
                    DisplayName = "Carol",
                    Roles = "user",
                    TokenVersion = 1
                });
            }
            db.SaveChanges();
        });

        return base.CreateHost(builder);
    }
}
