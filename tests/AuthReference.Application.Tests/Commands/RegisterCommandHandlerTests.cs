using AuthReference.Application.Commands.Register;
using AuthReference.Application.Notifications;
using AuthReference.Application.Tests.Fakes;
using AuthReference.Domain.Entities;
using AuthReference.Domain.Models.Requests;
using AuthReference.Domain.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace AuthReference.Application.Tests.Commands;

public class RegisterCommandHandlerTests
{
    [Fact]
    public async Task Success_IssuesTokens_StoresRefreshTokenHash_PublishesEvent()
    {
        var clock = new FakeClock(new DateTimeOffset(2026, 7, 6, 12, 0, 0, TimeSpan.Zero));
        var issuer = new FakeTokenIssuer(clock);
        var store = new FakeRefreshTokenStore();
        var bus = new CapturingPublisher();
        var registrar = new Mock<IUserRegistrar>();

        var alice = new ApplicationUser
        {
            Email = "alice@example.com",
            PasswordHash = "test:secret-2026",
            DisplayName = "Alice"
        };
        registrar.Setup(r => r.RegisterAsync(It.IsAny<RegisterRequest>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(RegisterOutcome.Ok(alice));

        var handler = new RegisterCommandHandler(
            registrar.Object, issuer, store, bus, clock, NullLogger<RegisterCommandHandler>.Instance);

        var result = await handler.Handle(
            new RegisterCommand(alice.Email, "secret-2026-strong-pw", alice.DisplayName), default);

        result.UserId.Should().Be(alice.Id);
        result.Tokens.AccessToken.Should().StartWith("at:");
        store.All.Should().HaveCount(1);
        bus.Published.Should().ContainSingle(n => n is UserRegisteredNotification);
    }

    [Fact]
    public async Task Failure_ThrowsInvalidOperation()
    {
        var clock = new FakeClock(DateTimeOffset.UtcNow);
        var registrar = new Mock<IUserRegistrar>();
        registrar.Setup(r => r.RegisterAsync(It.IsAny<RegisterRequest>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(RegisterOutcome.Failed("email already registered"));

        var handler = new RegisterCommandHandler(
            registrar.Object, new FakeTokenIssuer(clock), new FakeRefreshTokenStore(),
            new CapturingPublisher(), clock, NullLogger<RegisterCommandHandler>.Instance);

        var act = () => handler.Handle(
            new RegisterCommand("dup@example.com", "abcabcabcabc12", null), default);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already registered*");
    }
}
