using MediatR;

namespace AuthReference.Application.Tests.Fakes;

/// <summary>
/// Minimal <see cref="IPublisher"/> that just captures every notification.
/// Handlers under test typically publish domain-event notifications; tests
/// assert against <see cref="Published"/>.
/// </summary>
public sealed class CapturingPublisher : IPublisher
{
    public List<INotification> Published { get; } = new();

    public Task Publish(object notification, CancellationToken cancellationToken = default)
    {
        if (notification is INotification n) Published.Add(n);
        return Task.CompletedTask;
    }

    public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
        where TNotification : INotification
    {
        Published.Add(notification);
        return Task.CompletedTask;
    }
}
