using Microsoft.AspNetCore.Diagnostics.HealthChecks;

namespace AuthReference.Server.Endpoints;

public static class HealthEndpoints
{
    /// <summary>
    /// Two K8s-style probes:
    ///
    ///   /health/live   — the process is alive. Never touches downstreams.
    ///                    Reject only on catastrophic startup failure.
    ///   /health/ready  — every "ready" tagged health check passes. This is what
    ///                    the load balancer + orchestrator pin routing on.
    /// </summary>
    public static IEndpointRouteBuilder MapHealth(this IEndpointRouteBuilder app)
    {
        app.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = _ => false                                // no checks == liveness only
        }).AllowAnonymous();

        app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("ready")
        }).AllowAnonymous();

        return app;
    }
}
