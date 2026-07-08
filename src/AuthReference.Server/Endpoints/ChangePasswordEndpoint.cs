using System.Security.Claims;
using AuthReference.Application.Commands.ChangePassword;
using MediatR;

namespace AuthReference.Server.Endpoints;

public static class ChangePasswordEndpoint
{
    public const long MaxPayloadBytes = 8_000;

    // What the wire sees — the userId comes from the JWT, not the body.
    public record Body(string CurrentPassword, string NewPassword);

    public static IEndpointRouteBuilder MapChangePassword(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/auth/change-password", async (
                Body body, ClaimsPrincipal user, IMediator mediator, CancellationToken ct) =>
            {
                if (!Guid.TryParse(user.FindFirst("sub")?.Value ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value,
                                   out var userId))
                    return Results.Unauthorized();

                var ok = await mediator.Send(
                    new ChangePasswordCommand(userId, body.CurrentPassword, body.NewPassword), ct);
                return ok ? Results.NoContent() : Results.BadRequest(new { error = "password change denied" });
            })
            .RequireAuthorization()
            .WithName("ChangePassword")
            .WithSummary("Authenticated password change — bumps TokenVersion")
            .WithMetadata(new Microsoft.AspNetCore.Mvc.RequestSizeLimitAttribute(MaxPayloadBytes));

        return app;
    }
}
