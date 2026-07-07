using System.Collections.Concurrent;
using AuthReference.Domain.Services;

namespace AuthReference.Infrastructure.Services;

/// <summary>
/// Phase 3 placeholder — an in-process dictionary that goes away when the
/// process restarts. Fine for a single-node dev environment, obviously wrong
/// for a real multi-node deployment. Phase 4 swaps this out for a
/// <c>RedisTokenVersionStore</c> that survives restarts and shares state
/// across every replica of both the auth server and the resource servers.
/// </summary>
public sealed class InMemoryTokenVersionStore : ITokenVersionStore
{
    private readonly ConcurrentDictionary<Guid, int> _versions = new();

    public Task<int?> GetAsync(Guid userId, CancellationToken ct = default) =>
        Task.FromResult(_versions.TryGetValue(userId, out var v) ? (int?)v : null);

    public Task SetAsync(Guid userId, int version, CancellationToken ct = default)
    {
        _versions[userId] = version;
        return Task.CompletedTask;
    }

    public Task InvalidateAsync(Guid userId, CancellationToken ct = default)
    {
        _versions.TryRemove(userId, out _);
        return Task.CompletedTask;
    }
}
