using AuthReference.Application.Abstractions;
using AuthReference.Domain.Services;
using AuthReference.Infrastructure.Configuration;
using AuthReference.Infrastructure.OpenIddict;
using AuthReference.Infrastructure.Persistence;
using AuthReference.Infrastructure.Services;
using AuthReference.Infrastructure.Workers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace AuthReference.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Full stack for the IdP: DbContext, OpenIddict, every domain-interface
    /// implementation, retention worker, HeadlessRequestContext fallback.
    /// Server (Phase 4) uses this.
    /// </summary>
    public static IServiceCollection AddAuthReferenceInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddAuthReferenceCore(configuration);

        // --- OpenIddict core (managers + entity types) ---
        services.AddOpenIddict()
            .AddCore(o => o.UseEntityFrameworkCore().UseDbContext<AppDbContext>());

        // --- Full domain-interface implementations ---
        services.AddSingleton<IPasswordAuthenticator, Pbkdf2PasswordAuthenticator>();
        services.AddSingleton<ITokenIssuer, JwtTokenIssuer>();

        services.AddScoped<IUserRegistrar, EfUserRegistrar>();
        services.AddScoped<IUserActivityRecorder, EfUserActivityRecorder>();
        services.AddScoped<IPasswordChanger, EfPasswordChanger>();
        services.AddScoped<IRefreshTokenStore, PostgresRefreshTokenStore>();

        // --- Background workers ---
        services.AddHostedService<TokenRetentionWorker>();

        return services;
    }

    /// <summary>
    /// Minimal stack for a resource server: just what's needed to validate
    /// tokens issued by the IdP. Registers the DbContext (for the tv-claim
    /// cold-cache fallback), the TokenVersionStore (Redis when configured),
    /// the read-only user lookup, and the clock. Does NOT register the
    /// issuer, password hasher, write-side stores, OpenIddict, or the
    /// retention worker.
    /// Api (Phase 5) uses this.
    /// </summary>
    public static IServiceCollection AddAuthReferenceValidation(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddAuthReferenceCore(configuration);
        services.AddScoped<IUserLookup, EfUserLookup>();          // used by any per-request auth introspection
        return services;
    }

    /// <summary>
    /// Shared foundation both variants build on — options binding, DbContext,
    /// Redis or in-memory TokenVersionStore, clock, HeadlessRequestContext fallback.
    /// </summary>
    private static IServiceCollection AddAuthReferenceCore(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // --- Options ---
        services.Configure<InfrastructureOptions>(configuration.GetSection(InfrastructureOptions.SectionName));
        services.Configure<OpenIddictClientOptions>(configuration.GetSection(OpenIddictClientOptions.SectionName));

        // Fail fast if the connection string is missing.
        var connectionString = configuration[$"{InfrastructureOptions.SectionName}:Database:ConnectionString"]
            ?? throw new InvalidOperationException(
                $"Missing configuration: {InfrastructureOptions.SectionName}:Database:ConnectionString");

        // --- EF Core + Postgres ---
        services.AddDbContext<AppDbContext>(options =>
        {
            options.UseNpgsql(connectionString, npg =>
            {
                npg.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName);
                npg.EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: TimeSpan.FromSeconds(5), errorCodesToAdd: null);
                npg.CommandTimeout(configuration.GetValue($"{InfrastructureOptions.SectionName}:Database:CommandTimeoutSeconds", 30));
            });

            options.UseOpenIddict();       // shared across both variants — resource server may still enrich claims from OpenIddict apps
        });

        // --- Shared services ---
        services.AddSingleton<IClock, SystemClock>();
        services.AddScoped<IUserLookup, EfUserLookup>();

        // Token-version store: Redis when configured, in-memory otherwise.
        var redisConn = configuration[$"{InfrastructureOptions.SectionName}:Redis:ConnectionString"];
        if (!string.IsNullOrWhiteSpace(redisConn))
        {
            services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisConn));
            services.AddSingleton<ITokenVersionStore, RedisTokenVersionStore>();
        }
        else
        {
            services.AddSingleton<ITokenVersionStore, InMemoryTokenVersionStore>();
        }

        // Headless fallback for IRequestContext — Server + Api override with an HttpContext-backed impl.
        services.AddScoped<IRequestContext, HeadlessRequestContext>();

        return services;
    }

    /// <summary>
    /// Runs at host startup — creates the two demo OAuth applications if they
    /// do not exist. Idempotent, safe to call on every boot.
    /// </summary>
    public static async Task SeedOpenIddictClientsAsync(this IServiceProvider services, CancellationToken ct = default) =>
        await OpenIddictClientSeeder.SeedAsync(services, ct);
}

/// <summary>
/// Fallback <see cref="IRequestContext"/> for background workers, seeders,
/// integration-test bootstraps. Live HTTP requests get an
/// <c>HttpContextRequestContext</c> registered in the API host.
/// </summary>
internal sealed class HeadlessRequestContext : IRequestContext
{
    public string? IpAddress => null;
    public string? UserAgent => null;
    public string CorrelationId { get; } = Guid.NewGuid().ToString("N");
}
