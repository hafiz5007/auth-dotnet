namespace AuthReference.Infrastructure.Configuration;

/// <summary>
/// Root binding target for the "AuthReference" section of appsettings.
/// Keeping every knob in one place makes it easy to inspect what the service
/// actually reads from configuration.
/// </summary>
public class InfrastructureOptions
{
    public const string SectionName = "AuthReference";

    public DatabaseOptions Database { get; set; } = new();
    public JwtOptions Jwt { get; set; } = new();
    public PasswordOptions Password { get; set; } = new();
    public RedisOptions Redis { get; set; } = new();
    public RetentionOptions Retention { get; set; } = new();
}

public class DatabaseOptions
{
    /// <summary>Npgsql connection string. Required at startup; no silent fallback.</summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>Statement timeout in seconds. Defaults to 30.</summary>
    public int CommandTimeoutSeconds { get; set; } = 30;
}

public class JwtOptions
{
    public string Issuer { get; set; } = "https://auth-reference.local/";
    public string Audience { get; set; } = "auth-reference-api";

    /// <summary>
    /// HS256 signing key. Must be at least 32 bytes when treated as UTF-8.
    /// Production loads this from Key Vault / Secrets Manager, not appsettings.
    /// </summary>
    public string SigningKey { get; set; } = string.Empty;

    public int AccessTokenLifetimeMinutes { get; set; } = 10;
    public int RefreshTokenLifetimeDays { get; set; } = 14;
}

public class PasswordOptions
{
    /// <summary>PBKDF2 iteration count. 600k is the current OWASP guideline (2023+).</summary>
    public int Pbkdf2Iterations { get; set; } = 600_000;
}

public class RedisOptions
{
    /// <summary>StackExchange.Redis connection string, e.g. "localhost:6379".</summary>
    public string? ConnectionString { get; set; }

    /// <summary>Namespace prefix for every key this service writes. Isolates it from co-tenants.</summary>
    public string KeyPrefix { get; set; } = "auth-ref:";

    /// <summary>TTL for cached TokenVersion entries. Short — 60s balances freshness against DB load.</summary>
    public int TokenVersionTtlSeconds { get; set; } = 60;
}

public class RetentionOptions
{
    /// <summary>How often the retention worker sweeps. Defaults to once a day.</summary>
    public TimeSpan SweepInterval { get; set; } = TimeSpan.FromHours(24);

    /// <summary>Refresh-token retention window past expiry — keeps forensics available briefly.</summary>
    public TimeSpan RefreshTokenGrace { get; set; } = TimeSpan.FromDays(7);

    /// <summary>Audit-event retention window. FCA-style regulated services keep this longer.</summary>
    public TimeSpan AuditRetention { get; set; } = TimeSpan.FromDays(180);
}
