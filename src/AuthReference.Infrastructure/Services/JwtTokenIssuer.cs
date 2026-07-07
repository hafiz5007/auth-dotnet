using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using AuthReference.Domain.Entities;
using AuthReference.Domain.Models.Responses;
using AuthReference.Domain.Services;
using AuthReference.Infrastructure.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace AuthReference.Infrastructure.Services;

/// <summary>
/// Issues signed JWT access tokens and cryptographically-random opaque refresh
/// tokens. Uses HS256 for simplicity; production would switch to RS256 or
/// ES256 so resource servers only need the public key from JWKS.
///
/// The <c>tv</c> claim carries the user's <see cref="ApplicationUser.TokenVersion"/>
/// at issue time. Resource servers compare it against the current cached value
/// on every request; a mismatch means the token was invalidated (password
/// change, admin revoke) even though it's not yet expired.
/// </summary>
public sealed class JwtTokenIssuer : ITokenIssuer
{
    private readonly JwtOptions _jwt;
    private readonly IClock _clock;
    private readonly SigningCredentials _signing;

    public JwtTokenIssuer(IOptions<InfrastructureOptions> options, IClock clock)
    {
        _jwt = options.Value.Jwt;
        _clock = clock;

        if (string.IsNullOrEmpty(_jwt.SigningKey) || Encoding.UTF8.GetByteCount(_jwt.SigningKey) < 32)
        {
            throw new InvalidOperationException(
                "Jwt:SigningKey must be at least 32 bytes when encoded as UTF-8. " +
                "In production, load it from a secret store, not appsettings.");
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.SigningKey));
        _signing = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
    }

    public Task<TokenPair> IssueAsync(
        ApplicationUser user,
        IReadOnlyList<string> scopes,
        CancellationToken ct = default)
    {
        var now = _clock.UtcNow;
        var accessExpires = now.AddMinutes(_jwt.AccessTokenLifetimeMinutes);
        var refreshExpires = now.AddDays(_jwt.RefreshTokenLifetimeDays);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            new("tv", user.TokenVersion.ToString()),           // token-version — the revocation linchpin
            new("scope", string.Join(' ', scopes))
        };

        if (!string.IsNullOrEmpty(user.DisplayName))
            claims.Add(new Claim(JwtRegisteredClaimNames.Name, user.DisplayName));

        // Roles: comma-separated column → one "role" claim per entry.
        foreach (var role in user.Roles.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            claims.Add(new Claim(ClaimTypes.Role, role));

        var token = new JwtSecurityToken(
            issuer: _jwt.Issuer,
            audience: _jwt.Audience,
            claims: claims,
            notBefore: now.UtcDateTime,
            expires: accessExpires.UtcDateTime,
            signingCredentials: _signing);

        var accessJwt = new JwtSecurityTokenHandler().WriteToken(token);
        var refreshOpaque = GenerateRefreshToken();

        return Task.FromResult(new TokenPair(
            AccessToken: accessJwt,
            AccessTokenExpiresAtUtc: accessExpires,
            RefreshToken: refreshOpaque,
            RefreshTokenExpiresAtUtc: refreshExpires));
    }

    /// <summary>256 bits of CSPRNG randomness, URL-safe base64.</summary>
    private static string GenerateRefreshToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }
}
