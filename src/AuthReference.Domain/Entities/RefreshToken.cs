namespace AuthReference.Domain.Entities;

/// <summary>
/// Persisted refresh-token record. Rotation is enforced by <c>ReplacedById</c>
/// forming a chain — if the client presents a token that already has a
/// replacement, we treat that as a stolen-token event and revoke the chain.
/// </summary>
public class RefreshToken
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public required Guid UserId { get; init; }

    /// <summary>SHA256 of the token string. Raw string is never persisted.</summary>
    public required string TokenHash { get; init; }

    public DateTimeOffset IssuedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    public required DateTimeOffset ExpiresAtUtc { get; init; }

    public DateTimeOffset? RevokedAtUtc { get; set; }

    /// <summary>Populated when this token is rotated by a refresh call.</summary>
    public Guid? ReplacedById { get; set; }

    /// <summary>Free-text tag for post-mortem forensics ("rotated", "reuse-detected", "logout").</summary>
    public string? RevokeReason { get; set; }

    public bool IsActive(DateTimeOffset now) =>
        RevokedAtUtc is null && ExpiresAtUtc > now;
}
