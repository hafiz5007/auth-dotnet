using AuthReference.Domain.Entities;
using AuthReference.Domain.Models.Responses;
using AuthReference.Domain.Services;

namespace AuthReference.Application.Tests.Fakes;

/// <summary>
/// Deterministic issuer — access token is "at:{userId}:{counter}", refresh is
/// "rt:{userId}:{counter}". Every call increments the counter so consecutive
/// issuances produce different values (important for the rotation tests).
/// </summary>
public sealed class FakeTokenIssuer : ITokenIssuer
{
    private readonly IClock _clock;
    private int _counter;

    public FakeTokenIssuer(IClock clock) => _clock = clock;

    public Task<TokenPair> IssueAsync(
        ApplicationUser user,
        IReadOnlyList<string> scopes,
        CancellationToken ct = default)
    {
        _counter++;
        var pair = new TokenPair(
            AccessToken: $"at:{user.Id}:{_counter}",
            AccessTokenExpiresAtUtc: _clock.UtcNow.AddMinutes(10),
            RefreshToken: $"rt:{user.Id}:{_counter}",
            RefreshTokenExpiresAtUtc: _clock.UtcNow.AddDays(14));
        return Task.FromResult(pair);
    }
}
