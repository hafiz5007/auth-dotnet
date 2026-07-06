using AuthReference.Domain.Services;

namespace AuthReference.Application.Tests.Fakes;

public sealed class FakeUserActivityRecorder : IUserActivityRecorder
{
    public List<(Guid UserId, DateTimeOffset When, string? Ip)> Recorded { get; } = new();

    public Task RecordLoginAsync(Guid userId, DateTimeOffset whenUtc, string? ipAddress, CancellationToken ct = default)
    {
        Recorded.Add((userId, whenUtc, ipAddress));
        return Task.CompletedTask;
    }
}
