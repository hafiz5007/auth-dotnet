using AuthReference.Domain.Entities;
using AuthReference.Domain.Services;

namespace AuthReference.Application.Tests.Fakes;

public sealed class FakeRefreshTokenStore : IRefreshTokenStore
{
    // Keyed by TokenHash for realistic lookup semantics.
    private readonly Dictionary<string, RefreshToken> _tokens = new();

    public IReadOnlyDictionary<string, RefreshToken> All => _tokens;

    public Task AddAsync(RefreshToken token, CancellationToken ct = default)
    {
        _tokens[token.TokenHash] = token;
        return Task.CompletedTask;
    }

    public Task<RefreshToken?> FindActiveByHashAsync(string tokenHash, CancellationToken ct = default)
    {
        var token = _tokens.GetValueOrDefault(tokenHash);
        // Match the real store's contract: return null when revoked / expired.
        if (token is null || token.RevokedAtUtc is not null || token.ExpiresAtUtc <= DateTimeOffset.UtcNow)
            return Task.FromResult<RefreshToken?>(null);
        return Task.FromResult<RefreshToken?>(token);
    }

    public Task<bool> TryRotateAsync(RefreshToken presented, RefreshToken replacement, CancellationToken ct = default)
    {
        if (!_tokens.TryGetValue(presented.TokenHash, out var stored)) return Task.FromResult(false);
        if (stored.ReplacedById is not null) return Task.FromResult(false);
        stored.ReplacedById = replacement.Id;
        stored.RevokedAtUtc = DateTimeOffset.UtcNow;
        stored.RevokeReason = "rotated";
        _tokens[replacement.TokenHash] = replacement;
        return Task.FromResult(true);
    }

    public Task RevokeAllForUserAsync(Guid userId, string reason, CancellationToken ct = default)
    {
        foreach (var t in _tokens.Values.Where(t => t.UserId == userId && t.RevokedAtUtc is null))
        {
            t.RevokedAtUtc = DateTimeOffset.UtcNow;
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
