using AuthReference.Application.Commands.Refresh;
using AuthReference.Domain.Models.Enums;
using AuthReference.Server.RateLimiting;
using MediatR;

namespace AuthReference.Server.Endpoints;

public static class RefreshEndpoint
{
    public const long MaxPayloadBytes = 2_000;   // refresh body carries one opaque token

    public static IEndpointRouteBuilder MapRefresh(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/auth/refresh", async (RefreshCommand cmd, IMediator mediator, CancellationToken ct) =>
            {
                var response = await mediator.Send(cmd, ct);
                if (response.Decision == AuthDecision.Granted)
                    return Results.Ok(response.Tokens);

                // TokenReused is a security event, but externally still just 401.
                // The alert path is the RefreshTokenReuseDetected notification.
                return Results.Unauthorized();
            })
            .AllowAnonymous()
            .WithName("Refresh")
            .WithSummary("Rotate a refresh token — with reuse detection")
            .WithMetadata(new Microsoft.AspNetCore.Mvc.RequestSizeLimitAttribute(MaxPayloadBytes))
            .RequireRateLimiting(AuthRateLimitPolicies.Refresh);

        return app;
    }
}
