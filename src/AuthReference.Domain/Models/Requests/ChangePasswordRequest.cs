namespace AuthReference.Domain.Models.Requests;

/// <summary>
/// Authenticated request: caller supplies the current password to prevent CSRF-shaped
/// takeover if their session cookie leaks. On success we bump the user's TokenVersion
/// which invalidates every other outstanding access token.
/// </summary>
public record ChangePasswordRequest(string CurrentPassword, string NewPassword);
