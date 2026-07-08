using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace AuthReference.Server.RateLimiting;

/// <summary>
/// Named policies for every anonymous surface. Each partitions by client IP —
/// which is the attacker's identity on an anonymous endpoint, and the only
/// stable key we can extract without buffering the body.
///
///   auth-login     — 5 attempts / minute      (credential stuffing)
///   auth-register  — 3 attempts / hour        (registration flood)
///   auth-refresh   — 60 attempts / minute     (legitimate refresh cadence)
///
/// Mirrors the same shape as the rate limits in MM.Auth; the specific numbers
/// are the current OWASP recommendations rather than any product-specific tuning.
/// </summary>
public static class AuthRateLimitPolicies
{
    public const string Login = "auth-login";
    public const string Register = "auth-register";
    public const string Refresh = "auth-refresh";

    public static void Configure(RateLimiterOptions options)
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

        options.AddPolicy<string>(Login, http =>
            IpFixedWindow(http, permits: 5, TimeSpan.FromMinutes(1)));

        options.AddPolicy<string>(Register, http =>
            IpFixedWindow(http, permits: 3, TimeSpan.FromHours(1)));

        options.AddPolicy<string>(Refresh, http =>
            IpFixedWindow(http, permits: 60, TimeSpan.FromMinutes(1)));
    }

    private static RateLimitPartition<string> IpFixedWindow(
        HttpContext http, int permits, TimeSpan window)
    {
        var ip = http.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter($"ip:{ip}", _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = permits,
            Window = window,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 0
        });
    }
}
