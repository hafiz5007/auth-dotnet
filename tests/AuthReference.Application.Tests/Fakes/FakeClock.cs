using AuthReference.Domain.Services;

namespace AuthReference.Application.Tests.Fakes;

/// <summary>Test double for <see cref="IClock"/>. Time only moves when you say so.</summary>
public sealed class FakeClock : IClock
{
    private DateTimeOffset _now;

    public FakeClock(DateTimeOffset start) => _now = start;

    public DateTimeOffset UtcNow => _now;

    public void Advance(TimeSpan by) => _now = _now.Add(by);

    public void Set(DateTimeOffset at) => _now = at;
}
