namespace AuthReference.Domain.Entities;

/// <summary>
/// The persisted representation of a user. Kept intentionally slim: only the
/// fields the auth service itself owns. Downstream services (profile, billing,
/// KYC) hold their own projections keyed by <see cref="Id"/>.
/// </summary>
/// <remarks>
/// <see cref="TokenVersion"/> is the linchpin of immediate access-token revocation.
/// Every issued access token carries a <c>tv</c> claim equal to this value at
/// issue time. Resource servers reject any token whose <c>tv</c> claim is lower
/// than the current stored version, so incrementing this field kills every
/// outstanding access token in one write.
/// </remarks>
public class ApplicationUser
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public required string Email { get; set; }

    public bool EmailVerified { get; set; }

    /// <summary>PBKDF2 / Argon2 hash. Never the plaintext password.</summary>
    public required string PasswordHash { get; set; }

    public string? DisplayName { get; set; }

    /// <summary>Comma-separated roles ("user", "admin"). Simple by design.</summary>
    public string Roles { get; set; } = "user";

    /// <summary>
    /// Bumped whenever the user's session is globally invalidated (password change,
    /// admin revoke-all, suspected compromise). Increments monotonically.
    /// </summary>
    public int TokenVersion { get; set; } = 1;

    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? LastLoginAtUtc { get; set; }
}
