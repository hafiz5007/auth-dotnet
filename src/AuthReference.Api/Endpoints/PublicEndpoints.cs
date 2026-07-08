namespace AuthReference.Api.Endpoints;

public static class PublicEndpoints
{
    public static IEndpointRouteBuilder MapPublic(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/public/ping", () => Results.Ok(new
            {
                message = "pong",
                auth = "not required"
            }))
            .AllowAnonymous()
            .WithName("Ping")
            .WithSummary("Anonymous liveness probe for smoke tests");

        return app;
    }
}
