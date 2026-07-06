using AuthReference.Domain.Entities;

namespace AuthReference.Domain.Services;

/// <summary>
/// Wraps password hashing + comparison. Deliberately separate from persistence
/// so the hasher can be swapped (PBKDF2 → Argon2id → OPAQUE) without touching
/// the user store, and so tests can stub it.
/// </summary>
public interface IPasswordAuthenticator
{
    /// <summary>Produce a storage-safe hash for a new password.</summary>
    string Hash(string password);

    /// <summary>Constant-time verification of a candidate password against a stored hash.</summary>
    bool Verify(ApplicationUser user, string candidatePassword);

    /// <summary>
    /// Returns true if the hash was produced with an older algorithm / parameters
    /// than the current default. Callers upgrade the hash on the next successful login.
    /// </summary>
    bool NeedsRehash(ApplicationUser user);
}
