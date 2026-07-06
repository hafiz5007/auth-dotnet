using AuthReference.Application.Abstractions;
using AuthReference.Application.Notifications;
using AuthReference.Domain.Events;
using AuthReference.Domain.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AuthReference.Application.Commands.ChangePassword;

/// <summary>
/// Password change. On success:
///   1. <see cref="IPasswordChanger"/> bumps <c>TokenVersion</c> in the DB.
///   2. <see cref="IPasswordChanger"/> also invalidates the cached version
///      (Infrastructure impl composes both actions atomically).
///   3. We publish <see cref="PasswordChangedNotification"/> so a welcome-back
///      / password-changed email fires off downstream.
///
/// Every previously-issued access token now fails validation on next use
/// because its <c>tv</c> claim is stale.
/// </summary>
public sealed class ChangePasswordCommandHandler : IRequestHandler<ChangePasswordCommand, bool>
{
    private readonly IPasswordChanger _changer;
    private readonly IUserLookup _users;
    private readonly IPublisher _bus;
    private readonly IRequestContext _requestContext;
    private readonly IClock _clock;
    private readonly ILogger<ChangePasswordCommandHandler> _log;

    public ChangePasswordCommandHandler(
        IPasswordChanger changer,
        IUserLookup users,
        IPublisher bus,
        IRequestContext requestContext,
        IClock clock,
        ILogger<ChangePasswordCommandHandler> log)
    {
        _changer = changer;
        _users = users;
        _bus = bus;
        _requestContext = requestContext;
        _clock = clock;
        _log = log;
    }

    public async Task<bool> Handle(ChangePasswordCommand cmd, CancellationToken ct)
    {
        var ok = await _changer.ChangeAsync(cmd.UserId, cmd.CurrentPassword, cmd.NewPassword, ct);
        if (!ok)
        {
            _log.LogInformation("Password change denied for user {UserId}", cmd.UserId);
            return false;
        }

        var user = await _users.FindByIdAsync(cmd.UserId, ct);
        if (user is not null)
        {
            await _bus.Publish(new PasswordChangedNotification(new PasswordChangedEvent(
                user.Id, user.Email, _requestContext.IpAddress, _clock.UtcNow)), ct);
        }

        _log.LogInformation("Password changed for user {UserId}", cmd.UserId);
        return true;
    }
}
