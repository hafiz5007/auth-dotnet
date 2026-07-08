using AuthReference.Application.Commands.Register;
using AuthReference.Server.RateLimiting;
using MediatR;

namespace AuthReference.Server.Endpoints;

public static class RegisterEndpoint
{
    public const long MaxPayloadBytes = 16_000;

    public static IEndpointRouteBuilder MapRegister(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/auth/register", async (RegisterCommand cmd, IMediator mediator, CancellationToken ct) =>
            {
                var response = await mediator.Send(cmd, ct);
                return Results.Created($"/api/users/{response.UserId}", response);
            })
            .AllowAnonymous()
            .WithName("Register")
            .WithSummary("Create a user and issue the first token pair")
            .WithMetadata(new Microsoft.AspNetCore.Mvc.RequestSizeLimitAttribute(MaxPayloadBytes))
            .RequireRateLimiting(AuthRateLimitPolicies.Register);

        return app;
    }
}
