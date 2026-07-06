using AuthReference.Domain.Events;
using MediatR;

namespace AuthReference.Application.Notifications;

/// <summary>
/// Thin MediatR wrappers around Domain events. Handlers register against these,
/// keeping the framework dependency out of Domain. Every command handler that
/// raises a domain event does so by <c>Publish</c>-ing one of these records.
/// </summary>
public record UserRegisteredNotification(UserRegisteredEvent Event) : INotification;

public record PasswordChangedNotification(PasswordChangedEvent Event) : INotification;

public record RefreshTokenReuseDetectedNotification(RefreshTokenReuseDetectedEvent Event) : INotification;

public record AllTokensRevokedNotification(AllTokensRevokedEvent Event) : INotification;
