using System.Security.Claims;

namespace AuthReference.Api.Endpoints;

public static class AdminEndpoints
{
    public static IEndpointRouteBuilder MapAdmin(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/admin/secret", (ClaimsPrincipal user) => Results.Ok(new
            {
                message = "Only callers with the admin role see this",
                caller = user.FindFirst("email")?.Value ?? user.FindFirst("sub")?.Value
            }))
            .RequireAuthorization(policy => policy.RequireRole("admin"))
            .WithName("AdminSecret")
            .WithSummary("Role-protected endpoint — demonstrates the JWT role claim");

        return app;
    }
}
