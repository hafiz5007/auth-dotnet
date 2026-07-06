using AuthReference.Application.Notifications;
using AuthReference.Domain.Events;
using AuthReference.Domain.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AuthReference.Application.Commands.RevokeAll;

/// <summary>
/// Kill switch. Two atomic effects — refresh tokens are revoked in the store,
/// and the user's <c>TokenVersion</c> is invalidated in the version cache so
/// every access token still in flight fails validation on the next resource-
/// server hit. The database <c>TokenVersion</c> bump happens inside the
/// <see cref="IUserRegistrar"/> / equivalent write path — Phase 3 wires it up.
/// </summary>
public sealed class RevokeAllCommandHandler : IRequestHandler<RevokeAllCommand, Unit>
{
    private readonly IRefreshTokenStore _refreshTokens;
    private readonly ITokenVersionStore _versionCache;
    private readonly IPublisher _bus;
    private readonly IClock _clock;
    private readonly ILogger<RevokeAllCommandHandler> _log;

    public RevokeAllCommandHandler(
        IRefreshTokenStore refreshTokens,
        ITokenVersionStore versionCache,
        IPublisher bus,
        IClock clock,
        ILogger<RevokeAllCommandHandler> log)
    {
        _refreshTokens = refreshTokens;
        _versionCache = versionCache;
        _bus = bus;
        _clock = clock;
        _log = log;
    }

    public async Task<Unit> Handle(RevokeAllCommand cmd, CancellationToken ct)
    {
        await _refreshTokens.RevokeAllForUserAsync(cmd.TargetUserId, cmd.Reason, ct);
        await _versionCache.InvalidateAsync(cmd.TargetUserId, ct);

        await _bus.Publish(new AllTokensRevokedNotification(new AllTokensRevokedEvent(
            cmd.TargetUserId, cmd.Reason, cmd.InvokingUserId, _clock.UtcNow)), ct);

        _log.LogInformation("All sessions revoked for user {UserId} ({Reason})", cmd.TargetUserId, cmd.Reason);
        return Unit.Value;
    }
}
