using AuthReference.Domain.Abstractions;

namespace AuthReference.Domain.Events;

/// <summary>Raised after a successful registration, before the response is returned.</summary>
public record UserRegisteredEvent(
    Guid UserId,
    string Email,
    string? DisplayName,
    DateTimeOffset OccurredAtUtc) : IDomainEvent;
