using System.Security.Claims;

namespace AuthReference.Api.Endpoints;

public static class ProfileEndpoints
{
    /// <summary>
    /// Any authenticated caller. Echoes the claims the resource server saw
    /// after JWT validation + tv-claim enforcement — handy for smoke-testing
    /// that a token issued by the Server passes on the Api.
    /// </summary>
    public static IEndpointRouteBuilder MapProfile(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/profile/me", (ClaimsPrincipal user) =>
            {
                var subject = user.FindFirst("sub")?.Value
                              ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var email = user.FindFirst("email")?.Value;
                var displayName = user.FindFirst("name")?.Value;
                var tokenVersion = user.FindFirst("tv")?.Value;
                var scopes = user.FindFirst("scope")?.Value?.Split(' ') ?? Array.Empty<string>();
                var roles = user.FindAll(ClaimTypes.Role).Select(c => c.Value).ToArray();

                return Results.Ok(new
                {
                    subject,
                    email,
                    displayName,
                    tokenVersion,
                    scopes,
                    roles
                });
            })
            .RequireAuthorization()
            .WithName("ProfileMe")
            .WithSummary("Return the claims embedded in the caller's access token");

        return app;
    }
}
