using AuthReference.Application.Commands.Login;
using AuthReference.Application.Tests.Fakes;
using AuthReference.Domain.Entities;
using AuthReference.Domain.Models.Enums;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AuthReference.Application.Tests.Commands;

public class LoginCommandHandlerTests
{
    private static readonly DateTimeOffset T0 = new(2026, 7, 6, 12, 0, 0, TimeSpan.Zero);

    private (LoginCommandHandler Handler, Harness H) Build()
    {
        var clock = new FakeClock(T0);
        var users = new FakeUserLookup();
        var passwords = new FakePasswordAuthenticator();
        var issuer = new FakeTokenIssuer(clock);
        var refreshTokens = new FakeRefreshTokenStore();
        var activity = new FakeUserActivityRecorder();
        var context = new FakeRequestContext();

        var handler = new LoginCommandHandler(
            users, passwords, issuer, refreshTokens, activity, context,
            NullLogger<LoginCommandHandler>.Instance);

        return (handler, new Harness(clock, users, refreshTokens, activity));
    }

    private record Harness(
        FakeClock Clock,
        FakeUserLookup Users,
        FakeRefreshTokenStore RefreshTokens,
        FakeUserActivityRecorder Activity);

    private static ApplicationUser SeededAlice()
    {
        return new ApplicationUser
        {
            Email = "alice@example.com",
            PasswordHash = "test:secretPassword",
            EmailVerified = true,
            DisplayName = "Alice"
        };
    }

    [Fact]
    public async Task ValidCredentials_ReturnsGrantedWithTokens()
    {
        var (handler, h) = Build();
        h.Users.Seed(SeededAlice());

        var result = await handler.Handle(new LoginCommand("alice@example.com", "secretPassword"), default);

        result.Decision.Should().Be(AuthDecision.Granted);
        result.Tokens.Should().NotBeNull();
        result.Tokens!.AccessToken.Should().StartWith("at:");
        result.Tokens!.RefreshToken.Should().StartWith("rt:");
    }

    [Fact]
    public async Task UnknownUser_ReturnsInvalidCredentials_NotUserNotFound()
    {
        var (handler, _) = Build();

        var result = await handler.Handle(new LoginCommand("nobody@example.com", "anything"), default);

        result.Decision.Should().Be(AuthDecision.InvalidCredentials);
        result.Tokens.Should().BeNull();
    }

    [Fact]
    public async Task WrongPassword_ReturnsInvalidCredentials()
    {
        var (handler, h) = Build();
        h.Users.Seed(SeededAlice());

        var result = await handler.Handle(new LoginCommand("alice@example.com", "wrong"), default);

        result.Decision.Should().Be(AuthDecision.InvalidCredentials);
    }

    [Fact]
    public async Task ValidCredentials_PersistsRefreshTokenAsHash()
    {
        var (handler, h) = Build();
        h.Users.Seed(SeededAlice());

        var result = await handler.Handle(new LoginCommand("alice@example.com", "secretPassword"), default);

        h.RefreshTokens.All.Should().HaveCount(1);
        var stored = h.RefreshTokens.All.Values.Single();
        stored.TokenHash.Should().NotBe(result.Tokens!.RefreshToken, "raw token must never be persisted");
        stored.TokenHash.Length.Should().Be(64, "SHA256 hex string");
    }

    [Fact]
    public async Task ValidCredentials_RecordsUserActivity()
    {
        var (handler, h) = Build();
        h.Users.Seed(SeededAlice());

        await handler.Handle(new LoginCommand("alice@example.com", "secretPassword"), default);

        h.Activity.Recorded.Should().HaveCount(1);
        h.Activity.Recorded[0].Ip.Should().Be("10.0.0.1");
    }
}
