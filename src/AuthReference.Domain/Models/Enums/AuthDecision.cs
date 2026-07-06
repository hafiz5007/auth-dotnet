namespace AuthReference.Domain.Models.Enums;

/// <summary>
/// Outcome of a password / token exchange. Kept as an explicit enum rather
/// than a boolean so downstream callers can differentiate rate-limited from
/// unknown-user without leaking user-existence information.
/// </summary>
public enum AuthDecision
{
    Granted,
    InvalidCredentials,
    UserNotFound,             // never returned externally — collapsed to InvalidCredentials at the API surface
    UserDisabled,
    RateLimited,
    TokenExpired,
    TokenReused,              // refresh-token reuse detected — chain revoked
    UnknownError
}
