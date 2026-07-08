using System.Net;
using AuthReference.Api.Tests.TestHarness;
using FluentAssertions;
using Xunit;

namespace AuthReference.Api.Tests;

public class HealthTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public HealthTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Live_ReturnsHealthy()
    {
        var response = await _factory.CreateClient().GetAsync("/health/live");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // /health/ready would exercise the DbContext check; since we're on InMemory
    // it should still succeed. If the InMemory provider ever complains about
    // the concurrency-token property, this test surfaces it early.
    [Fact]
    public async Task Ready_ReturnsHealthy()
    {
        var response = await _factory.CreateClient().GetAsync("/health/ready");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
