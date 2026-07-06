using AuthReference.Application.Commands.RevokeAll;
using AuthReference.Application.Notifications;
using AuthReference.Application.Tests.Fakes;
using AuthReference.Domain.Cryptography;
using AuthReference.Domain.Entities;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AuthReference.Application.Tests.Commands;

public class RevokeAllCommandHandlerTests
{
    [Fact]
    public async Task Revoke_KillsAllRefreshTokens_InvalidatesVersionCache_PublishesEvent()
    {
        var clock = new FakeClock(new DateTimeOffset(2026, 7, 6, 12, 0, 0, TimeSpan.Zero));
        var store = new FakeRefreshTokenStore();
        var versions = new FakeTokenVersionStore();
        var bus = new CapturingPublisher();

        var userId = Guid.NewGuid();
        await store.AddAsync(new RefreshToken
        {
            UserId = userId,
            TokenHash = RefreshTokenHasher.Hash("rt-a"),
            ExpiresAtUtc = clock.UtcNow.AddDays(7)
        }, default);
        await store.AddAsync(new RefreshToken
        {
            UserId = userId,
            TokenHash = RefreshTokenHasher.Hash("rt-b"),
            ExpiresAtUtc = clock.UtcNow.AddDays(7)
        }, default);

        var handler = new RevokeAllCommandHandler(
            store, versions, bus, clock, NullLogger<RevokeAllCommandHandler>.Instance);

        await handler.Handle(new RevokeAllCommand(userId, "admin-action", InvokingUserId: null), default);

        store.All.Values.Where(t => t.UserId == userId).Should().OnlyContain(t => t.RevokedAtUtc != null);
        versions.InvalidateCalls.Should().Be(1);
        bus.Published.Should().ContainSingle(n => n is AllTokensRevokedNotification);
    }
}
