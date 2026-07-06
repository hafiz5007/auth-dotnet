using AuthReference.Domain.Entities;
using AuthReference.Domain.Services;

namespace AuthReference.Application.Tests.Fakes;

public sealed class FakeUserLookup : IUserLookup
{
    private readonly Dictionary<string, ApplicationUser> _byEmail = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<Guid, ApplicationUser> _byId = new();

    public void Seed(ApplicationUser user)
    {
        _byEmail[user.Email] = user;
        _byId[user.Id] = user;
    }

    public Task<ApplicationUser?> FindByEmailAsync(string email, CancellationToken ct = default) =>
        Task.FromResult(_byEmail.GetValueOrDefault(email));

    public Task<ApplicationUser?> FindByIdAsync(Guid userId, CancellationToken ct = default) =>
        Task.FromResult(_byId.GetValueOrDefault(userId));
}
