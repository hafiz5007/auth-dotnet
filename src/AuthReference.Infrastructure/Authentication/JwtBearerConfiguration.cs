using System.Text;
using AuthReference.Domain.Services;
using AuthReference.Infrastructure.Configuration;
using AuthReference.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace AuthReference.Infrastructure.Authentication;

/// <summary>
/// Configures JWT bearer validation with the same signing key + issuer that
/// <c>JwtTokenIssuer</c> uses. Adds an event hook that enforces the
/// <c>tv</c> claim against the current cached <see cref="ITokenVersionStore"/>
/// value — mismatch → 401 with "token version stale".
///
/// Both the Server (IdP) and the Api (resource server) call this. The shared
/// implementation guarantees they interpret revocation the same way.
/// </summary>
public static class JwtBearerConfiguration
{
    public static IServiceCollection AddAuthReferenceJwtBearer(this IServiceCollection services)
    {
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.SaveToken = true;
            });

        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<InfrastructureOptions>>((jwt, infra) =>
            {
                var cfg = infra.Value.Jwt;
                jwt.RequireHttpsMetadata = false;                  // dev; prod appsettings flips this

                jwt.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = cfg.Issuer,
                    ValidAudience = cfg.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(cfg.SigningKey)),
                    ClockSkew = TimeSpan.FromSeconds(30)
                };

                jwt.Events = new JwtBearerEvents
                {
                    OnTokenValidated = TokenVersionCheck.OnTokenValidated
                };
            });

        return services;
    }
}

/// <summary>
/// The <c>tv</c>-claim enforcement. Every validated token carries a
/// token-version claim set at issue time; the cache holds the latest value
/// for the user. Mismatch → treat the token as revoked.
///
/// On a cache miss the check falls back to Postgres so revocation still
/// enforces on a cold cache — and warms the cache for subsequent reads.
/// </summary>
internal static class TokenVersionCheck
{
    public static async Task OnTokenValidated(TokenValidatedContext ctx)
    {
        var services = ctx.HttpContext.RequestServices;
        var versionStore = services.GetRequiredService<ITokenVersionStore>();

        var principal = ctx.Principal;
        if (principal is null) return;

        var sub = principal.FindFirst("sub")?.Value;
        var tvClaim = principal.FindFirst("tv")?.Value;

        if (!Guid.TryParse(sub, out var userId) || !int.TryParse(tvClaim, out var tokenVersion))
        {
            ctx.Fail("Malformed subject or token version claim.");
            return;
        }

        var current = await versionStore.GetAsync(userId, ctx.HttpContext.RequestAborted);
        if (current is null)
        {
            // Cache cold: fall back to the DB so revocation still enforces on first read.
            var db = services.GetRequiredService<AppDbContext>();
            current = await db.Users
                .Where(u => u.Id == userId)
                .Select(u => (int?)u.TokenVersion)
                .FirstOrDefaultAsync(ctx.HttpContext.RequestAborted);

            if (current is not null)
                await versionStore.SetAsync(userId, current.Value, ctx.HttpContext.RequestAborted);
        }

        if (current is null || tokenVersion < current)
            ctx.Fail("Token version stale — session was invalidated.");
    }
}
