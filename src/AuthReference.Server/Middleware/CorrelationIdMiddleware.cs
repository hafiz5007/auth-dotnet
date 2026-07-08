using Serilog.Context;

namespace AuthReference.Server.Middleware;

/// <summary>
/// Reads <c>X-Correlation-Id</c> off the incoming request (or mints one),
/// stamps it on the response, and pushes it into the Serilog log context so
/// every log line for this request is tagged with it.
/// </summary>
public sealed class CorrelationIdMiddleware
{
    public const string HeaderName = "X-Correlation-Id";
    public const string ItemsKey = "CorrelationId";

    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next) => _next = next;

    public async Task Invoke(HttpContext context)
    {
        var cid = context.Request.Headers.TryGetValue(HeaderName, out var incoming)
                    && !string.IsNullOrWhiteSpace(incoming.ToString())
                  ? incoming.ToString()
                  : Guid.NewGuid().ToString("N");

        context.Items[ItemsKey] = cid;
        context.Response.Headers[HeaderName] = cid;

        using (LogContext.PushProperty("CorrelationId", cid))
        {
            await _next(context);
        }
    }
}
