using AuthReference.Domain.Services;
using AuthReference.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AuthReference.Infrastructure.Services;

/// <summary>
/// Password change is one of the few operations in this service that mutates
/// <c>TokenVersion</c>. The two writes (password_hash + token_version) happen
/// in the same SaveChanges call so a mid-flight failure leaves neither half
/// applied. The token-version cache is then invalidated to force a re-read
/// on the next resource-server check.
/// </summary>
public sealed class EfPasswordChanger : IPasswordChanger
{
    private readonly AppDbContext _db;
    private readonly IPasswordAuthenticator _passwords;
    private readonly ITokenVersionStore _versionCache;
    private readonly IRefreshTokenStore _refreshTokens;

    public EfPasswordChanger(
        AppDbContext db,
        IPasswordAuthenticator passwords,
        ITokenVersionStore versionCache,
        IRefreshTokenStore refreshTokens)
    {
        _db = db;
        _passwords = passwords;
        _versionCache = versionCache;
        _refreshTokens = refreshTokens;
    }

    public async Task<bool> ChangeAsync(
        Guid userId,
        string currentPassword,
        string newPassword,
        CancellationToken ct = default)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null) return false;

        if (!_passwords.Verify(user, currentPassword)) return false;

        user.PasswordHash = _passwords.Hash(newPassword);
        user.TokenVersion += 1;                                // kills every access token in flight
        await _db.SaveChangesAsync(ct);

        await _versionCache.InvalidateAsync(userId, ct);
        await _refreshTokens.RevokeAllForUserAsync(userId, "password-changed", ct);

        return true;
    }
}
