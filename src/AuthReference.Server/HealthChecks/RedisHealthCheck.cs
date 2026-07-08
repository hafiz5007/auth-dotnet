using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;

namespace AuthReference.Server.HealthChecks;

/// <summary>
/// Registered only when Redis is configured; a missing Redis connection is a
/// wire-up decision made at DI time, so its absence is not a health-check
/// failure — the fallback in-memory store is used instead.
/// </summary>
public sealed class RedisHealthCheck : IHealthCheck
{
    private readonly IConnectionMultiplexer _redis;

    public RedisHealthCheck(IConnectionMultiplexer redis) => _redis = redis;

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var latency = await _redis.GetDatabase().PingAsync();
            return HealthCheckResult.Healthy(
                description: $"redis ping ok in {latency.TotalMilliseconds:F0}ms");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("redis ping failed", ex);
        }
    }
}
