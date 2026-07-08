using AuthReference.Domain.Services;
using AuthReference.Infrastructure.Configuration;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace AuthReference.Infrastructure.Services;

/// <summary>
/// Redis-backed <see cref="ITokenVersionStore"/>. Every access-token check on
/// a resource server hits this — the cache absorbs the load that would
/// otherwise fall on Postgres.
///
///  Key shape:  <c>{prefix}tv:{userId}</c>  →  string value of the integer version.
///  Absence means "unknown to the cache"; the caller falls back to Postgres.
///
/// A short TTL (default 60s) keeps stale reads bounded. Any TokenVersion
/// mutation writes the new value AND explicitly deletes the key so a
/// concurrent read repopulates from the DB.
/// </summary>
public sealed class RedisTokenVersionStore : ITokenVersionStore
{
    private readonly IDatabase _db;
    private readonly string _prefix;
    private readonly TimeSpan _ttl;

    public RedisTokenVersionStore(IConnectionMultiplexer redis, IOptions<InfrastructureOptions> options)
    {
        _db = redis.GetDatabase();
        var cfg = options.Value.Redis;
        _prefix = cfg.KeyPrefix + "tv:";
        _ttl = TimeSpan.FromSeconds(cfg.TokenVersionTtlSeconds);
    }

    public async Task<int?> GetAsync(Guid userId, CancellationToken ct = default)
    {
        var value = await _db.StringGetAsync(Key(userId));
        if (value.IsNullOrEmpty) return null;
        return int.TryParse(value.ToString(), out var v) ? v : null;
    }

    public async Task SetAsync(Guid userId, int version, CancellationToken ct = default) =>
        await _db.StringSetAsync(Key(userId), version.ToString(), _ttl);

    public async Task InvalidateAsync(Guid userId, CancellationToken ct = default) =>
        await _db.KeyDeleteAsync(Key(userId));

    private RedisKey Key(Guid userId) => _prefix + userId.ToString("N");
}
