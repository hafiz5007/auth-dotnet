using AuthReference.Domain.Abstractions;

namespace AuthReference.Domain.Events;

/// <summary>Fires after a successful password change. Triggers session invalidation + notification email.</summary>
public record PasswordChangedEvent(
    Guid UserId,
    string Email,
    string? IpAddress,
    DateTimeOffset OccurredAtUtc) : IDomainEvent;
