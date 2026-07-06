namespace AuthReference.Domain.Entities;

/// <summary>
/// Immutable audit-trail row. One per interesting auth event: login, failed login,
/// token refresh, revoke-all, password change, registration. Retention policy is
/// enforced by the retention worker in Infrastructure.
/// </summary>
public class AuthAuditEvent
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public Guid? UserId { get; init; }

    public required string EventType { get; init; }         // LOGIN_OK, LOGIN_FAIL, REFRESH, ...

    public string? Detail { get; init; }                    // free-text: "wrong password", "ip mismatch"

    public string? IpAddress { get; init; }

    public string? UserAgent { get; init; }

    public string? CorrelationId { get; init; }

    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
