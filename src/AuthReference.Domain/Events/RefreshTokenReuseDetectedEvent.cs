using AuthReference.Domain.Abstractions;

namespace AuthReference.Domain.Events;

/// <summary>
/// Somebody presented a refresh token that had already been rotated. Almost
/// always signals theft — the legitimate client would only ever hold the newest
/// token in the chain. Handler responds by revoking every session for the user
/// and alerting the security team.
/// </summary>
public record RefreshTokenReuseDetectedEvent(
    Guid UserId,
    Guid PresentedTokenId,
    string? IpAddress,
    string? UserAgent,
    DateTimeOffset OccurredAtUtc) : IDomainEvent;
