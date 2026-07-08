using System.Net;
using System.Net.Http.Headers;
using AuthReference.Api.Tests.TestHarness;
using AuthReference.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AuthReference.Api.Tests;

public class ProtectedEndpointsTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public ProtectedEndpointsTests(ApiFactory factory) => _factory = factory;

    // ── /api/public/ping — anonymous ────────────────────────────

    [Fact]
    public async Task PublicPing_ReturnsOk_WithoutAuth()
    {
        var response = await _factory.CreateClient().GetAsync("/api/public/ping");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ── /api/profile/me — requires auth ─────────────────────────

    [Fact]
    public async Task ProfileMe_Returns401_WithoutToken()
    {
        var response = await _factory.CreateClient().GetAsync("/api/profile/me");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ProfileMe_Returns200_WithValidToken()
    {
        var token = TokenBuilder.Build(ApiFactory.AliceId, tokenVersion: 1, roles: new[] { "user" });
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/profile/me");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain(ApiFactory.AliceId.ToString());
    }

    // ── /api/admin/secret — requires "admin" role ──────────────

    [Fact]
    public async Task AdminSecret_Returns403_ForNonAdmin()
    {
        var token = TokenBuilder.Build(ApiFactory.AliceId, tokenVersion: 1, roles: new[] { "user" });
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/admin/secret");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AdminSecret_Returns200_ForAdmin()
    {
        var token = TokenBuilder.Build(ApiFactory.BobId, tokenVersion: 1, roles: new[] { "user", "admin" });
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/admin/secret");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ── tv-claim enforcement ────────────────────────────────────

    [Fact]
    public async Task ProfileMe_Returns401_WhenTokenVersionStaleVsDb()
    {
        // Bump Alice's TokenVersion in the DB while her outstanding token still says tv=1.
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Users
                .Where(u => u.Id == ApiFactory.AliceId)
                .ExecuteUpdateAsync(s => s.SetProperty(u => u.TokenVersion, 2));
        }

        var staleToken = TokenBuilder.Build(ApiFactory.AliceId, tokenVersion: 1, roles: new[] { "user" });
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", staleToken);

        var response = await client.GetAsync("/api/profile/me");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
