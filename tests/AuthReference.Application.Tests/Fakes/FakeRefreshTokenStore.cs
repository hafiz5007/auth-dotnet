using AuthReference.Domain.Entities;
using AuthReference.Domain.Services;

namespace AuthReference.Application.Tests.Fakes;

public sealed class FakeRefreshTokenStore : IRefreshTokenStore
{
    // Keyed by TokenHash for realistic lookup semantics.
    private readonly Dictionary<string, RefreshToken> _tokens = new();
    private readonly IClock _clock;

    public FakeRefreshTokenStore(IClock clock) => _clock = clock;

    public IReadOnlyDictionary<string, RefreshToken> All => _tokens;

    public Task AddAsync(RefreshToken token, CancellationToken ct = default)
    {
        _tokens[token.TokenHash] = token;
        return Task.CompletedTask;
    }

    public Task<RefreshToken?> FindActiveByHashAsync(string tokenHash, CancellationToken ct = default)
    {
        var token = _tokens.GetValueOrDefault(tokenHash);
        // Match the real store's contract: null only when unknown / expired. Revoked
        // and rotated rows are returned so the handler can drive reuse detection.
        if (token is null || token.ExpiresAtUtc <= _clock.UtcNow)
            return Task.FromResult<RefreshToken?>(null);
        return Task.FromResult<RefreshToken?>(token);
    }

    public Task<bool> TryRotateAsync(RefreshToken presented, RefreshToken replacement, CancellationToken ct = default)
    {
        if (!_tokens.TryGetValue(presented.TokenHash, out var stored)) return Task.FromResult(false);
        if (stored.ReplacedById is not null) return Task.FromResult(false);
        stored.ReplacedById = replacement.Id;
        stored.RevokedAtUtc = _clock.UtcNow;
        stored.RevokeReason = "rotated";
        _tokens[replacement.TokenHash] = replacement;
        return Task.FromResult(true);
    }

    public Task RevokeAllForUserAsync(Guid userId, string reason, CancellationToken ct = default)
    {
        foreach (var t in _tokens.Values.Where(t => t.UserId == userId && t.RevokedAtUtc is null))
        {
            t.RevokedAtUtc = _clock.UtcNow;
            t.RevokeReason = reason;
        }
        return Task.CompletedTask;
    }

    public Task<int> PruneExpiredAsync(DateTimeOffset olderThanUtc, CancellationToken ct = default)
    {
        var toRemove = _tokens.Values
            .Where(t => t.ExpiresAtUtc < olderThanUtc)
            .Select(t => t.TokenHash)
            .ToList();
        foreach (var hash in toRemove) _tokens.Remove(hash);
        return Task.FromResult(toRemove.Count);
    }
}
