using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace AuthReference.Api.Tests.TestHarness;

/// <summary>
/// Mints test JWTs signed with the same key <see cref="ApiFactory"/> configures.
/// Lets each test specify exactly the claim set it wants.
/// </summary>
public static class TokenBuilder
{
    private static readonly SigningCredentials Signing = new(
        new SymmetricSecurityKey(Encoding.UTF8.GetBytes(ApiFactory.TestSigningKey)),
        SecurityAlgorithms.HmacSha256);

    public static string Build(
        Guid subject,
        int tokenVersion,
        string[] roles,
        string email = "alice@example.com",
        TimeSpan? lifetime = null,
        string? issuer = null,
        string? audience = null)
    {
        var claims = new List<Claim>
        {
            new("sub", subject.ToString()),
            new("email", email),
            new("tv", tokenVersion.ToString()),
            new("scope", "openid profile email api"),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N"))
        };
        foreach (var r in roles) claims.Add(new Claim(ClaimTypes.Role, r));

        var token = new JwtSecurityToken(
            issuer: issuer ?? "https://test-issuer.local/",
            audience: audience ?? "auth-reference-api",
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: DateTime.UtcNow.Add(lifetime ?? TimeSpan.FromMinutes(10)),
            signingCredentials: Signing);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
