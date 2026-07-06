namespace AuthReference.Domain.Models.Requests;

/// <summary>
/// Admin-scope revoke: kill every outstanding session for the target user.
/// Reason is captured in the audit trail.
/// </summary>
public record RevokeAllRequest(Guid UserId, string Reason);
