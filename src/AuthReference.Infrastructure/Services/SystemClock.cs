using AuthReference.Domain.Services;

namespace AuthReference.Infrastructure.Services;

/// <summary>Real-clock implementation used in production. Tests use FakeClock.</summary>
public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
