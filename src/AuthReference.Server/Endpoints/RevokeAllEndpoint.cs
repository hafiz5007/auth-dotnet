using System.Security.Claims;
using AuthReference.Application.Commands.RevokeAll;
using MediatR;

namespace AuthReference.Server.Endpoints;

public static class RevokeAllEndpoint
{
    public record Body(Guid TargetUserId, string Reason);

    public static IEndpointRouteBuilder MapRevokeAll(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/auth/admin/revoke-all", async (
                Body body, ClaimsPrincipal caller, IMediator mediator, CancellationToken ct) =>
            {
                Guid? invokingUserId = Guid.TryParse(
                    caller.FindFirst("sub")?.Value ?? caller.FindFirst(ClaimTypes.NameIdentifier)?.Value,
                    out var g) ? g : null;

                await mediator.Send(new RevokeAllCommand(body.TargetUserId, body.Reason, invokingUserId), ct);
                return Results.NoContent();
            })
            .RequireAuthorization(policy => policy.RequireRole("admin"))
            .WithName("RevokeAll")
            .WithSummary("Admin: kill every session for a user (bumps TokenVersion)");

        return app;
    }
}
