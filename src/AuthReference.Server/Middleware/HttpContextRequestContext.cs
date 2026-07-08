using AuthReference.Application.Abstractions;

namespace AuthReference.Server.Middleware;

/// <summary>
/// Wire-up: <see cref="IRequestContext"/> reads live values off the current
/// <see cref="HttpContext"/>. Registered as scoped so it walks a fresh
/// <see cref="IHttpContextAccessor"/> read per request.
/// </summary>
public sealed class HttpContextRequestContext : IRequestContext
{
    private readonly IHttpContextAccessor _accessor;

    public HttpContextRequestContext(IHttpContextAccessor accessor) => _accessor = accessor;

    public string? IpAddress => _accessor.HttpContext?.Connection.RemoteIpAddress?.ToString();

    public string? UserAgent => _accessor.HttpContext?.Request.Headers.UserAgent.ToString();

    public string CorrelationId =>
        _accessor.HttpContext?.Items[CorrelationIdMiddleware.ItemsKey] as string
        ?? Guid.NewGuid().ToString("N");
}
