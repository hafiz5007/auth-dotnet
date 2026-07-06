using AuthReference.Application.Notifications;
using AuthReference.Domain.Cryptography;
using AuthReference.Domain.Entities;
using AuthReference.Domain.Events;
using AuthReference.Domain.Models.Requests;
using AuthReference.Domain.Models.Responses;
using AuthReference.Domain.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AuthReference.Application.Commands.Register;

/// <summary>
/// Registration + immediate first token issuance. Publishes
/// <see cref="UserRegisteredNotification"/> so downstream consumers (welcome
/// email, analytics) can react without touching this handler.
/// </summary>
public sealed class RegisterCommandHandler : IRequestHandler<RegisterCommand, RegisterResponse>
{
    private readonly IUserRegistrar _registrar;
    private readonly ITokenIssuer _tokens;
    private readonly IRefreshTokenStore _refreshTokens;
    private readonly IPublisher _bus;
    private readonly IClock _clock;
    private readonly ILogger<RegisterCommandHandler> _log;

    public RegisterCommandHandler(
        IUserRegistrar registrar,
        ITokenIssuer tokens,
        IRefreshTokenStore refreshTokens,
        IPublisher bus,
        IClock clock,
        ILogger<RegisterCommandHandler> log)
    {
        _registrar = registrar;
        _tokens = tokens;
        _refreshTokens = refreshTokens;
        _bus = bus;
        _clock = clock;
        _log = log;
    }

    public async Task<RegisterResponse> Handle(RegisterCommand cmd, CancellationToken ct)
    {
        var outcome = await _registrar.RegisterAsync(
            new RegisterRequest(cmd.Email, cmd.Password, cmd.DisplayName), ct);

        if (!outcome.Success || outcome.User is null)
            throw new InvalidOperationException(outcome.Error ?? "Registration failed");

        var tokens = await _tokens.IssueAsync(outcome.User, DefaultScopes, ct);

        await _refreshTokens.AddAsync(new RefreshToken
        {
            UserId = outcome.User.Id,
            TokenHash = RefreshTokenHasher.Hash(tokens.RefreshToken),
            ExpiresAtUtc = tokens.RefreshTokenExpiresAtUtc
        }, ct);

        await _bus.Publish(new UserRegisteredNotification(new UserRegisteredEvent(
            outcome.User.Id, outcome.User.Email, outcome.User.DisplayName, _clock.UtcNow)), ct);

        _log.LogInformation("Registered user {UserId}", outcome.User.Id);
        return new RegisterResponse(outcome.User.Id, outcome.User.Email, tokens);
    }

    private static readonly IReadOnlyList<string> DefaultScopes = new[] { "openid", "profile", "email", "api" };
}
