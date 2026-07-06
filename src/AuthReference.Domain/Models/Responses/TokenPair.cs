namespace AuthReference.Domain.Models.Responses;

/// <summary>
/// Access + refresh token pair. The access token is short-lived and carries a
/// <c>tv</c> claim matching <see cref="Entities.ApplicationUser.TokenVersion"/>
/// at issue time. The refresh token is opaque; its SHA256 hash is what's stored.
/// </summary>
public record TokenPair(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAtUtc,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAtUtc);
