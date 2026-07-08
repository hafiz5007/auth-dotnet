using AuthReference.Domain.Entities;
using AuthReference.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AuthReference.Api.Tests.TestHarness;

/// <summary>
/// Boots the Api host in-process with two swaps for test friendliness:
///  * DbContext is redirected to an EF Core InMemory provider (no Postgres running).
///  * The Redis-backed TokenVersionStore is not registered (no Redis running),
///    so the in-memory fallback kicks in.
///
/// A seeded user is added on startup so tests can mint valid tokens against a
/// stable known subject id.
/// </summary>
public class ApiFactory : WebApplicationFactory<Program>
{
    public const string TestSigningKey = "test-only-32-byte-signing-key-!!!";
    public static readonly Guid AliceId = Guid.Parse("11111111-2222-3333-4444-555555555555");
    public static readonly Guid BobId   = Guid.Parse("22222222-3333-4444-5555-666666666666");

    protected override IHost CreateHost(IHostBuilder builder)
    {
        builder.ConfigureAppConfiguration(cfg =>
        {
            cfg.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AuthReference:Database:ConnectionString"] = "Host=localhost;Database=test;Username=x;Password=x",
                ["AuthReference:Jwt:SigningKey"] = TestSigningKey,
                ["AuthReference:Jwt:Issuer"] = "https://test-issuer.local/",
                ["AuthReference:Jwt:Audience"] = "auth-reference-api",
                // no Redis — InMemoryTokenVersionStore falls back
                ["AuthReference:Redis:ConnectionString"] = null
            }!);
        });

        builder.ConfigureServices(services =>
        {
            // Swap AppDbContext to InMemory. The Postgres registration is removed
            // and a fresh InMemory one is added under the same DI key.
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
            if (descriptor is not null) services.Remove(descriptor);

            services.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase("api-tests"));

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
            db.SaveChanges();
        });

        return base.CreateHost(builder);
    }
}
