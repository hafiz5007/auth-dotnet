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
    /// Wires every infrastructure concern in one call. Server and Api hosts do:
    /// <code>
    /// services.AddAuthReferenceApplication();
    /// services.AddAuthReferenceInfrastructure(builder.Configuration);
    /// </code>
    /// </summary>
    public static IServiceCollection AddAuthReferenceInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // --- Options ---
        services.Configure<InfrastructureOptions>(configuration.GetSection(InfrastructureOptions.SectionName));
        services.Configure<OpenIddictClientOptions>(configuration.GetSection(OpenIddictClientOptions.SectionName));

        // Fail fast if the connection string is missing — a silent fallback would
        // land production writes on the wrong database (see MM.Auth AH-4 comment).
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

            // Hook OpenIddict's entity sets into our DbContext. Running
            // `dotnet ef migrations add …` after this is registered will
            // include the OpenIddict tables alongside our own.
            options.UseOpenIddict();
        });

        // --- OpenIddict core (managers + entity types) ---
        services.AddOpenIddict()
            .AddCore(o => o.UseEntityFrameworkCore().UseDbContext<AppDbContext>());

        // --- Domain-interface implementations ---
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IPasswordAuthenticator, Pbkdf2PasswordAuthenticator>();
        services.AddSingleton<ITokenIssuer, JwtTokenIssuer>();

        // Token-version store: Redis when a connection string is configured, in-memory otherwise.
        // Multi-node deployments MUST configure Redis — otherwise revocation won't propagate
        // between the replica that bumped TokenVersion and the replica that validates tokens.
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

        services.AddScoped<IUserLookup, EfUserLookup>();
        services.AddScoped<IUserRegistrar, EfUserRegistrar>();
        services.AddScoped<IUserActivityRecorder, EfUserActivityRecorder>();
        services.AddScoped<IPasswordChanger, EfPasswordChanger>();
        services.AddScoped<IRefreshTokenStore, PostgresRefreshTokenStore>();

        // --- Application abstractions that Infrastructure fills ---
        // A HeadlessRequestContext is registered as a last-resort fallback for
        // background workers + integration tests. The Server host overrides
        // this registration with an HttpContext-backed implementation.
        services.AddScoped<IRequestContext, HeadlessRequestContext>();

        // --- Background workers ---
        services.AddHostedService<TokenRetentionWorker>();

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
/// Fallback <see cref="IRequestContext"/> used when no HTTP request is in
/// flight (background workers, seeder, integration tests). Every field is
/// deliberately null — a real request wires <c>HttpContextRequestContext</c>
/// in Phase 4.
/// </summary>
internal sealed class HeadlessRequestContext : IRequestContext
{
    public string? IpAddress => null;
    public string? UserAgent => null;
    public string CorrelationId { get; } = Guid.NewGuid().ToString("N");
}
