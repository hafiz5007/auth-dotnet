using AuthReference.Application.Abstractions;

namespace AuthReference.Application.Tests.Fakes;

public sealed class FakeRequestContext : IRequestContext
{
    public string? IpAddress { get; init; } = "10.0.0.1";
    public string? UserAgent { get; init; } = "xUnit";
    public string CorrelationId { get; init; } = Guid.NewGuid().ToString("N");
}
