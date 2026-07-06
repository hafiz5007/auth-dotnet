namespace AuthReference.Domain.Abstractions;

/// <summary>
/// Marker for events raised inside the domain. Application-layer notifications
/// (MediatR <c>INotification</c>) wrap these; Domain stays framework-free.
/// </summary>
public interface IDomainEvent
{
    DateTimeOffset OccurredAtUtc { get; }
}
