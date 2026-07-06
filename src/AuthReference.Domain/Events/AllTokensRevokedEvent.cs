using AuthReference.Domain.Abstractions;

namespace AuthReference.Domain.Events;

/// <summary>
/// Every session for a user was killed. Triggered by admin action, password
/// change, or reuse-detection response. Reason is captured for the audit trail.
/// </summary>
public record AllTokensRevokedEvent(
    Guid UserId,
    string Reason,
    Guid? RevokedByUserId,
    DateTimeOffset OccurredAtUtc) : IDomainEvent;
