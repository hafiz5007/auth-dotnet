using AuthReference.Domain.Entities;
using AuthReference.Domain.Services;

namespace AuthReference.Application.Tests.Fakes;

/// <summary>
/// Not a real hasher — passwords are stored as "test:{plaintext}" so the tests
/// can assert against known values without dealing with hash format churn.
/// </summary>
public sealed class FakePasswordAuthenticator : IPasswordAuthenticator
{
    public string Hash(string password) => "test:" + password;

    public bool Verify(ApplicationUser user, string candidatePassword) =>
        user.PasswordHash == "test:" + candidatePassword;

    public bool NeedsRehash(ApplicationUser user) => !user.PasswordHash.StartsWith("test:");
}
