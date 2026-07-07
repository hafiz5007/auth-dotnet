using AuthReference.Domain.Entities;
using AuthReference.Domain.Services;
using AuthReference.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AuthReference.Infrastructure.Services;

/// <summary>
/// Postgres-backed refresh-token store. Two subtleties worth calling out:
///
///   1. <see cref="FindActiveByHashAsync"/> deliberately returns rows that
///      have already been replaced — so the caller can see the
///      <c>ReplacedById</c> and detect reuse. Callers that only want
///      currently-valid tokens must check <see cref="RefreshToken.IsActive"/>.
///
///   2. <see cref="TryRotateAsync"/> uses <c>ExecuteUpdateAsync</c> with a
///      predicate that only matches when the token is still un-replaced.
///      That gives us optimistic concurrency without a full concurrency
///      token — if two rotations race, exactly one lands the update.
/// </summary>
public sealed class PostgresRefreshTokenStore : IRefreshTokenStore
{
    private readonly AppDbContext _db;
    private readonly IClock _clock;

    public PostgresRefreshTokenStore(AppDbContext db, IClock clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task AddAsync(RefreshToken token, CancellationToken ct = default)
    {
        _db.RefreshTokens.Add(token);
        await _db.SaveChangesAsync(ct);
    }

    public Task<RefreshToken?> FindActiveByHashAsync(string tokenHash, CancellationToken ct = default) =>
        // "Active" here means "not expired". Revoked / rotated rows are returned
        // so the caller can inspect ReplacedById and drive reuse detection.
        _db.RefreshTokens
            .AsTracking()
            .Where(t => t.TokenHash == tokenHash && t.ExpiresAtUtc > _clock.UtcNow)
            .FirstOrDefaultAsync(ct);

    public async Task<bool> TryRotateAsync(
        RefreshToken presented,
        RefreshToken replacement,
        CancellationToken ct = default)
    {
        // Atomic "replace if still un-replaced" — Postgres will only match rows
        // whose replaced_by_id IS NULL, so a concurrent rotate loses the race.
        var affected = await _db.RefreshTokens
            .Where(t => t.Id == presented.Id && t.ReplacedById == null)
            .ExecuteUpdateAsync(setter => setter
                .SetProperty(t => t.ReplacedById, replacement.Id)
                .SetProperty(t => t.RevokedAtUtc, _clock.UtcNow)
                .SetProperty(t => t.RevokeReason, "rotated"), ct);

        if (affected == 0) return false;

        _db.RefreshTokens.Add(replacement);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public Task RevokeAllForUserAsync(Guid userId, string reason, CancellationToken ct = default) =>
        _db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAtUtc == null)
            .ExecuteUpdateAsync(setter => setter
                .SetProperty(t => t.RevokedAtUtc, _clock.UtcNow)
                .SetProperty(t => t.RevokeReason, reason), ct);

    public async Task<int> PruneExpiredAsync(DateTimeOffset olderThanUtc, CancellationToken ct = default) =>
        await _db.RefreshTokens
            .Where(t => t.ExpiresAtUtc < olderThanUtc)
            .ExecuteDeleteAsync(ct);
}
