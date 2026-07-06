namespace AuthReference.Domain.Services;

/// <summary>
/// The password-change use case has its own service because it triggers a
/// side effect (bump <c>TokenVersion</c>) that non-password writes must not.
/// Callers should already be authenticated when they invoke this.
/// </summary>
public interface IPasswordChanger
{
    /// <summary>
    /// Verify current password, hash new password, persist, bump TokenVersion,
    /// invalidate cached version. Returns true on success.
    /// </summary>
    Task<bool> ChangeAsync(
        Guid userId,
        string currentPassword,
        string newPassword,
        CancellationToken ct = default);
}
