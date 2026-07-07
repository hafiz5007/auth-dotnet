using System.Security.Cryptography;
using System.Text;
using AuthReference.Domain.Entities;
using AuthReference.Domain.Services;
using AuthReference.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace AuthReference.Infrastructure.Services;

/// <summary>
/// PBKDF2-HMAC-SHA256 password hasher. Hash format:
///
///     $pbkdf2-sha256$&lt;iterations&gt;$&lt;base64-salt&gt;$&lt;base64-hash&gt;
///
/// Verifying uses a constant-time comparison. If a stored hash was produced
/// with an older iteration count, <see cref="NeedsRehash"/> returns true so
/// the login handler can upgrade the hash silently on next successful login.
///
/// Chosen over BCrypt because PBKDF2 is in the BCL (no NuGet), and over
/// Argon2id only because Argon2 requires a well-maintained package that has
/// historically been under-updated. In production for real fintech I would
/// use Argon2id via a vetted library.
/// </summary>
public sealed class Pbkdf2PasswordAuthenticator : IPasswordAuthenticator
{
    private const string Prefix = "$pbkdf2-sha256$";
    private const int SaltBytes = 16;
    private const int HashBytes = 32;

    private readonly int _currentIterations;

    public Pbkdf2PasswordAuthenticator(IOptions<InfrastructureOptions> options)
    {
        _currentIterations = options.Value.Password.Pbkdf2Iterations;
    }

    public string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltBytes);
        var hash = Derive(password, salt, _currentIterations);
        return $"{Prefix}{_currentIterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    public bool Verify(ApplicationUser user, string candidatePassword)
    {
        if (!TryParse(user.PasswordHash, out var iterations, out var salt, out var expected))
            return false;

        var computed = Derive(candidatePassword, salt, iterations);
        return CryptographicOperations.FixedTimeEquals(computed, expected);
    }

    public bool NeedsRehash(ApplicationUser user) =>
        !TryParse(user.PasswordHash, out var iterations, out _, out _) ||
        iterations < _currentIterations;

    private static byte[] Derive(string password, byte[] salt, int iterations) =>
        Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password),
            salt,
            iterations,
            HashAlgorithmName.SHA256,
            HashBytes);

    private static bool TryParse(string encoded, out int iterations, out byte[] salt, out byte[] hash)
    {
        iterations = 0; salt = Array.Empty<byte>(); hash = Array.Empty<byte>();
        if (string.IsNullOrEmpty(encoded) || !encoded.StartsWith(Prefix)) return false;

        var parts = encoded[Prefix.Length..].Split('$');
        if (parts.Length != 3) return false;

        if (!int.TryParse(parts[0], out iterations) || iterations < 1) return false;

        try
        {
            salt = Convert.FromBase64String(parts[1]);
            hash = Convert.FromBase64String(parts[2]);
        }
        catch (FormatException) { return false; }

        return true;
    }
}
