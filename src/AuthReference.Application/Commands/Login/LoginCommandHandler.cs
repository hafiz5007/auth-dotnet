using AuthReference.Application.Abstractions;
using AuthReference.Domain.Cryptography;
using AuthReference.Domain.Entities;
using AuthReference.Domain.Models.Enums;
using AuthReference.Domain.Models.Responses;
using AuthReference.Domain.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AuthReference.Application.Commands.Login;

/// <summary>
/// Password grant. Deliberately collapses "user not found" and "wrong password"
/// into the same public failure so a probe cannot enumerate registered emails.
/// </summary>
public sealed class LoginCommandHandler : IRequestHandler<LoginCommand, LoginResponse>
{
    private readonly IUserLookup _users;
    private readonly IPasswordAuthenticator _passwords;
    private readonly ITokenIssuer _tokens;
    private readonly IRefreshTokenStore _refreshTokens;
    private readonly IUserActivityRecorder _activity;
    private readonly IRequestContext _requestContext;
    private readonly ILogger<LoginCommandHandler> _log;

    public LoginCommandHandler(
        IUserLookup users,
        IPasswordAuthenticator passwords,
        ITokenIssuer tokens,
        IRefreshTokenStore refreshTokens,
        IUserActivityRecorder activity,
        IRequestContext requestContext,
        ILogger<LoginCommandHandler> log)
    {
        _users = users;
        _passwords = passwords;
        _tokens = tokens;
        _refreshTokens = refreshTokens;
        _activity = activity;
        _requestContext = requestContext;
        _log = log;
    }

    public async Task<LoginResponse> Handle(LoginCommand cmd, CancellationToken ct)
    {
        var user = await _users.FindByEmailAsync(cmd.Email, ct);
        if (user is null)
        {
            _log.LogInformation("Login denied — unknown email");
            return LoginResponse.Denied(AuthDecision.InvalidCredentials);
        }

        if (!_passwords.Verify(user, cmd.Password))
        {
            _log.LogInformation("Login denied — wrong password for user {UserId}", user.Id);
            return LoginResponse.Denied(AuthDecision.InvalidCredentials);
        }

        var tokens = await _tokens.IssueAsync(user, DefaultScopes, ct);

        // Persist the refresh token hash — the raw string never touches the DB.
        var refreshRecord = new RefreshToken
        {
            UserId = user.Id,
            TokenHash = RefreshTokenHasher.Hash(tokens.RefreshToken),
            ExpiresAtUtc = tokens.RefreshTokenExpiresAtUtc
        };
        await _refreshTokens.AddAsync(refreshRecord, ct);

        await _activity.RecordLoginAsync(user.Id, tokens.AccessTokenExpiresAtUtc, _requestContext.IpAddress, ct);

        _log.LogInformation("Login granted for user {UserId}", user.Id);
        return LoginResponse.Granted(tokens);
    }

    private static readonly IReadOnlyList<string> DefaultScopes = new[] { "openid", "profile", "email", "api" };
}
