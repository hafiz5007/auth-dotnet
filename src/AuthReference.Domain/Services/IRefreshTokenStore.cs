using AuthReference.Domain.Entities;

namespace AuthReference.Domain.Services;

/// <summary>
/// Persistence for refresh tokens. The store is authoritative; a fast-path
/// cache in Infrastructure (Redis) is a separate concern behind the same
/// abstraction — implementations are free to combine them.
/// </summary>
public interface IRefreshTokenStore
{
    /// <summary>Persist a freshly issued token. Never called with the raw token — always the hash.</summary>
    Task AddAsync(RefreshToken token, CancellationToken ct = default);

    /// <summary>Look up by SHA256 hash. Returns null if unknown, revoked, or expired.</summary>
    Task<RefreshToken?> FindActiveByHashAsync(string tokenHash, CancellationToken ct = default);

    /// <summary>
    /// Atomically mark the presented token as replaced-by the new one. If the
    /// presented token was already replaced, that's a stolen-token event —
    /// callers should invoke <see cref="RevokeAllForUserAsync"/> as the response.
    /// </summary>
    Task<bool> TryRotateAsync(
        RefreshToken presented,
        RefreshToken replacement,
        CancellationToken ct = default);

    /// <summary>Kill every outstanding refresh token for a user with an audit reason.</summary>
    Task RevokeAllForUserAsync(Guid userId, string reason, CancellationToken ct = default);

    /// <summary>Delete expired tokens older than the retention window. Called by the retention worker.</summary>
    Task<int> PruneExpiredAsync(DateTimeOffset olderThanUtc, CancellationToken ct = default);
}
