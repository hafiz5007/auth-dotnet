using AuthReference.Domain.Entities;
using AuthReference.Domain.Models.Requests;

namespace AuthReference.Domain.Services;

/// <summary>
/// Owns the "new user" write path. Separate from the authentication path so
/// registration policies (email verification, invitation tokens, quota checks)
/// can evolve without touching login.
/// </summary>
public interface IUserRegistrar
{
    Task<RegisterOutcome> RegisterAsync(RegisterRequest request, CancellationToken ct = default);
}

/// <summary>Result of a registration attempt.</summary>
public record RegisterOutcome(bool Success, ApplicationUser? User, string? Error)
{
    public static RegisterOutcome Ok(ApplicationUser user) => new(true, user, null);
    public static RegisterOutcome Failed(string error) => new(false, null, error);
}
