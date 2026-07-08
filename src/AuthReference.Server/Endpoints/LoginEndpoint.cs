using AuthReference.Application.Commands.Login;
using AuthReference.Domain.Models.Enums;
using AuthReference.Server.RateLimiting;
using MediatR;

namespace AuthReference.Server.Endpoints;

public static class LoginEndpoint
{
    public const long MaxPayloadBytes = 8_000;   // login body is tiny; hard cap defends against oversized JSON

    public static IEndpointRouteBuilder MapLogin(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/auth/login", async (LoginCommand cmd, IMediator mediator, CancellationToken ct) =>
            {
                var response = await mediator.Send(cmd, ct);
                return response.Decision == AuthDecision.Granted
                    ? Results.Ok(response.Tokens)
                    : Results.Unauthorized();
            })
            .AllowAnonymous()
            .WithName("Login")
            .WithSummary("Exchange email + password for a token pair")
            .WithMetadata(new Microsoft.AspNetCore.Http.Metadata.RequestSizeLimitMetadata(MaxPayloadBytes))
            .RequireRateLimiting(AuthRateLimitPolicies.Login);

        return app;
    }
}
