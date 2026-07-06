using AuthReference.Domain.Services;

namespace AuthReference.Application.Tests.Fakes;

public sealed class FakeTokenVersionStore : ITokenVersionStore
{
    private readonly Dictionary<Guid, int> _versions = new();
    public int InvalidateCalls { get; private set; }

    public Task<int?> GetAsync(Guid userId, CancellationToken ct = default) =>
        Task.FromResult(_versions.TryGetValue(userId, out var v) ? (int?)v : null);

    public Task SetAsync(Guid userId, int version, CancellationToken ct = default)
    {
        _versions[userId] = version;
        return Task.CompletedTask;
    }

    public Task InvalidateAsync(Guid userId, CancellationToken ct = default)
    {
        _versions.Remove(userId);
        InvalidateCalls++;
        return Task.CompletedTask;
    }
}
