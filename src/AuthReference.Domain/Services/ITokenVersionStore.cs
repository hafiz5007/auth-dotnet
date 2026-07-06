namespace AuthReference.Domain.Services;

/// <summary>
/// Fast-path read of the current <c>TokenVersion</c> for a user. Resource servers
/// hit this on every request (backed by Redis with a short TTL). The database
/// remains authoritative — the cache is a read accelerator.
/// </summary>
/// <remarks>
/// This is the plumbing behind immediate access-token revocation. When a token
/// is validated, the resource server compares the token's <c>tv</c> claim against
/// the value returned here. Mismatch → 401. On any TokenVersion bump, the store
/// is invalidated so the next read reflects the new value.
/// </remarks>
public interface ITokenVersionStore
{
    /// <summary>Return the current token version for a user. Null when the user is unknown.</summary>
    Task<int?> GetAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Update the cached version and invalidate any stale copy.</summary>
    Task SetAsync(Guid userId, int version, CancellationToken ct = default);

    /// <summary>Drop the cached version. Next read repopulates from the database.</summary>
    Task InvalidateAsync(Guid userId, CancellationToken ct = default);
}
