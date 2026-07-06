using AuthReference.Application.Abstractions;
using AuthReference.Application.Notifications;
using AuthReference.Domain.Cryptography;
using AuthReference.Domain.Entities;
using AuthReference.Domain.Events;
using AuthReference.Domain.Models.Enums;
using AuthReference.Domain.Models.Responses;
using AuthReference.Domain.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AuthReference.Application.Commands.Refresh;

/// <summary>
/// Refresh-token rotation with reuse detection.
///
/// The presented token is looked up by its SHA256 hash. Three outcomes:
///   1. Unknown / expired  → deny.
///   2. Active + never rotated → issue a new pair, mark the presented one replaced.
///   3. Already replaced   → REUSE. Someone else has been using the chain — revoke
///                          every token for the user and publish a security event.
/// </summary>
public sealed class RefreshCommandHandler : IRequestHandler<RefreshCommand, LoginResponse>
{
    private readonly IRefreshTokenStore _store;
    private readonly IUserLookup _users;
    private readonly ITokenIssuer _issuer;
    private readonly IPublisher _bus;
    private readonly IClock _clock;
    private readonly IRequestContext _requestContext;
    private readonly ILogger<RefreshCommandHandler> _log;

    public RefreshCommandHandler(
        IRefreshTokenStore store,
        IUserLookup users,
        ITokenIssuer issuer,
        IPublisher bus,
        IClock clock,
        IRequestContext requestContext,
        ILogger<RefreshCommandHandler> log)
    {
        _store = store;
        _users = users;
        _issuer = issuer;
        _bus = bus;
        _clock = clock;
        _requestContext = requestContext;
        _log = log;
    }

    public async Task<LoginResponse> Handle(RefreshCommand cmd, CancellationToken ct)
    {
        var presentedHash = RefreshTokenHasher.Hash(cmd.RefreshToken);
        var presented = await _store.FindActiveByHashAsync(presentedHash, ct);

        if (presented is null)
        {
            _log.LogInformation("Refresh denied — token unknown or expired");
            return LoginResponse.Denied(AuthDecision.TokenExpired);
        }

        // Reuse detection: if we ever find a token that's already been replaced,
        // the chain has been forked — treat as compromise and revoke everything.
        if (presented.ReplacedById is not null)
        {
            _log.LogWarning("Refresh-token REUSE detected for user {UserId} — revoking all sessions", presented.UserId);

            await _store.RevokeAllForUserAsync(presented.UserId, "refresh-token reuse detected", ct);

            await _bus.Publish(new RefreshTokenReuseDetectedNotification(new RefreshTokenReuseDetectedEvent(
                presented.UserId, presented.Id, _requestContext.IpAddress, _requestContext.UserAgent, _clock.UtcNow)), ct);

            return LoginResponse.Denied(AuthDecision.TokenReused);
        }

        var user = await _users.FindByIdAsync(presented.UserId, ct);
        if (user is null)
        {
            // User deleted since token was issued — treat as expired.
            return LoginResponse.Denied(AuthDecision.TokenExpired);
        }

        var newPair = await _issuer.IssueAsync(user, DefaultScopes, ct);
        var replacement = new RefreshToken
        {
            UserId = user.Id,
            TokenHash = RefreshTokenHasher.Hash(newPair.RefreshToken),
            ExpiresAtUtc = newPair.RefreshTokenExpiresAtUtc
        };

        var rotated = await _store.TryRotateAsync(presented, replacement, ct);
        if (!rotated)
        {
            // Race: someone rotated the token between our read and our write. Rare
            // and always safe to treat as reuse — the caller can just retry with
            // the new token if they were the legitimate rotater.
            _log.LogWarning("Refresh rotation lost the race for user {UserId}", user.Id);
            return LoginResponse.Denied(AuthDecision.TokenReused);
        }

        _log.LogInformation("Refresh granted for user {UserId}", user.Id);
        return LoginResponse.Granted(newPair);
    }

    private static readonly IReadOnlyList<string> DefaultScopes = new[] { "openid", "profile", "email", "api" };
}
