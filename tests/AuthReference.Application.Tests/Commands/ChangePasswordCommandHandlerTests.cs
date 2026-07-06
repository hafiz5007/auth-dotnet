using AuthReference.Application.Commands.ChangePassword;
using AuthReference.Application.Notifications;
using AuthReference.Application.Tests.Fakes;
using AuthReference.Domain.Entities;
using AuthReference.Domain.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace AuthReference.Application.Tests.Commands;

public class ChangePasswordCommandHandlerTests
{
    [Fact]
    public async Task Success_PublishesPasswordChanged()
    {
        var clock = new FakeClock(new DateTimeOffset(2026, 7, 6, 12, 0, 0, TimeSpan.Zero));
        var users = new FakeUserLookup();
        var alice = new ApplicationUser { Email = "alice@example.com", PasswordHash = "test:old" };
        users.Seed(alice);

        var changer = new Mock<IPasswordChanger>();
        changer.Setup(c => c.ChangeAsync(alice.Id, "old", "brand-new-secret-2026", It.IsAny<CancellationToken>()))
               .ReturnsAsync(true);

        var bus = new CapturingPublisher();

        var handler = new ChangePasswordCommandHandler(
            changer.Object, users, bus, new FakeRequestContext(), clock,
            NullLogger<ChangePasswordCommandHandler>.Instance);

        var ok = await handler.Handle(new ChangePasswordCommand(alice.Id, "old", "brand-new-secret-2026"), default);

        ok.Should().BeTrue();
        bus.Published.Should().ContainSingle(n => n is PasswordChangedNotification);
    }

    [Fact]
    public async Task Denial_DoesNotPublishEvent()
    {
        var clock = new FakeClock(DateTimeOffset.UtcNow);
        var users = new FakeUserLookup();
        var userId = Guid.NewGuid();

        var changer = new Mock<IPasswordChanger>();
        changer.Setup(c => c.ChangeAsync(userId, "wrong", "new-password-2026", It.IsAny<CancellationToken>()))
               .ReturnsAsync(false);

        var bus = new CapturingPublisher();

        var handler = new ChangePasswordCommandHandler(
            changer.Object, users, bus, new FakeRequestContext(), clock,
            NullLogger<ChangePasswordCommandHandler>.Instance);

        var ok = await handler.Handle(new ChangePasswordCommand(userId, "wrong", "new-password-2026"), default);

        ok.Should().BeFalse();
        bus.Published.Should().BeEmpty();
    }
}
