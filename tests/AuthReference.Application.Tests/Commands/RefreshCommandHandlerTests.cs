using AuthReference.Application.Commands.Refresh;
using AuthReference.Application.Notifications;
using AuthReference.Application.Tests.Fakes;
using AuthReference.Domain.Cryptography;
using AuthReference.Domain.Entities;
using AuthReference.Domain.Models.Enums;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AuthReference.Application.Tests.Commands;

public class RefreshCommandHandlerTests
{
    private static readonly DateTimeOffset T0 = new(2026, 7, 6, 12, 0, 0, TimeSpan.Zero);

    private (RefreshCommandHandler Handler, FakeClock Clock, FakeUserLookup Users,
             FakeRefreshTokenStore Store, CapturingPublisher Bus) Build()
    {
        var clock = new FakeClock(T0);
        var users = new FakeUserLookup();
        var store = new FakeRefreshTokenStore(clock);
        var issuer = new FakeTokenIssuer(clock);
        var bus = new CapturingPublisher();
        var handler = new RefreshCommandHandler(
            store, users, issuer, bus, clock, new FakeRequestContext(),
            NullLogger<RefreshCommandHandler>.Instance);
        return (handler, clock, users, store, bus);
    }

    private static ApplicationUser Alice() => new()
    {
        Email = "alice@example.com",
        PasswordHash = "test:x"
    };

    [Fact]
    public async Task UnknownToken_ReturnsTokenExpired()
    {
        var (handler, _, _, _, _) = Build();

        var result = await handler.Handle(new RefreshCommand("no-such-token"), default);

        result.Decision.Should().Be(AuthDecision.TokenExpired);
    }

    [Fact]
    public async Task ValidToken_ReturnsNewPair_AndMarksOldRotated()
    {
        var (handler, clock, users, store, _) = Build();
        var alice = Alice();
        users.Seed(alice);
        var rawOld = "rt-1234-abc";
        await store.AddAsync(new RefreshToken
        {
            UserId = alice.Id,
            TokenHash = RefreshTokenHasher.Hash(rawOld),
            ExpiresAtUtc = clock.UtcNow.AddDays(7)
        }, default);

        var result = await handler.Handle(new RefreshCommand(rawOld), default);

        result.Decision.Should().Be(AuthDecision.Granted);
        result.Tokens!.RefreshToken.Should().NotBe(rawOld);

        var stored = store.All.Values.Single(t => t.TokenHash == RefreshTokenHasher.Hash(rawOld));
        stored.ReplacedById.Should().NotBeNull("old token should be marked as rotated");
        stored.RevokedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task ReusedToken_RevokesEverything_AndPublishesSecurityEvent()
    {
        var (handler, clock, users, store, bus) = Build();
        var alice = Alice();
        users.Seed(alice);
        var rawOld = "rt-1234-abc";
        await store.AddAsync(new RefreshToken
        {
            UserId = alice.Id,
            TokenHash = RefreshTokenHasher.Hash(rawOld),
            ExpiresAtUtc = clock.UtcNow.AddDays(7)
        }, default);

        // First refresh — legitimate rotation.
        await handler.Handle(new RefreshCommand(rawOld), default);

        // Someone else (attacker) presents the same old token.
        var result = await handler.Handle(new RefreshCommand(rawOld), default);

        result.Decision.Should().Be(AuthDecision.TokenReused);

        bus.Published.Should().Contain(n => n is RefreshTokenReuseDetectedNotification);

        // Every token belonging to alice should be revoked now.
        store.All.Values.Where(t => t.UserId == alice.Id).Should().OnlyContain(t => t.RevokedAtUtc != null);
    }

    [Fact]
    public async Task AfterReuseDetected_NewestTokenInChain_IsDenied()
    {
        var (handler, clock, users, store, _) = Build();
        var alice = Alice();
        users.Seed(alice);
        var rawOld = "rt-1234-abc";
        await store.AddAsync(new RefreshToken
        {
            UserId = alice.Id,
            TokenHash = RefreshTokenHasher.Hash(rawOld),
            ExpiresAtUtc = clock.UtcNow.AddDays(7)
        }, default);

        var first = await handler.Handle(new RefreshCommand(rawOld), default);
        await handler.Handle(new RefreshCommand(rawOld), default);   // reuse → revoke all

        // The rotated-to token was never replaced, only revoked — it must not refresh.
        var result = await handler.Handle(new RefreshCommand(first.Tokens!.RefreshToken), default);

        result.Decision.Should().Be(AuthDecision.TokenExpired);
        result.Tokens.Should().BeNull();
    }

    [Fact]
    public async Task RevokedToken_IsDenied_AndNotRotated()
    {
        var (handler, clock, users, store, _) = Build();
        var alice = Alice();
        users.Seed(alice);
        var raw = "rt-revoked";
        await store.AddAsync(new RefreshToken
        {
            UserId = alice.Id,
            TokenHash = RefreshTokenHasher.Hash(raw),
            ExpiresAtUtc = clock.UtcNow.AddDays(7)
        }, default);
        await store.RevokeAllForUserAsync(alice.Id, "revoke-all", default);

        var result = await handler.Handle(new RefreshCommand(raw), default);

        result.Decision.Should().Be(AuthDecision.TokenExpired);
        store.All.Values.Single(t => t.TokenHash == RefreshTokenHasher.Hash(raw)).ReplacedById.Should().BeNull();
        store.All.Should().HaveCount(1, "no replacement token should be issued");
    }
}
