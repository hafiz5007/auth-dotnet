using System.Security.Cryptography;
using System.Text;

namespace AuthReference.Domain.Cryptography;

/// <summary>
/// One-way hash used before any refresh-token string is persisted. The raw
/// token string only ever exists on the wire and in the client's cookie /
/// storage; the server persists <c>SHA256(token)</c> so a database dump does
/// not equal a session takeover.
/// </summary>
/// <remarks>
/// SHA256 (not bcrypt / argon2) is the right choice here — the input is
/// already a 256-bit cryptographically random value from
/// <c>RandomNumberGenerator.GetBytes(32)</c>, so a fast hash is fine and a
/// slow adaptive hash would only add latency. Uses only BCL types so the
/// helper stays inside the framework-free Domain layer.
/// </remarks>
public static class RefreshTokenHasher
{
    public static string Hash(string token)
    {
        if (string.IsNullOrEmpty(token)) throw new ArgumentException("token must not be empty", nameof(token));
        var bytes = Encoding.UTF8.GetBytes(token);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }
}
