using AuthReference.Domain.Entities;

namespace AuthReference.Domain.Services;

/// <summary>Read side of the user store. Used by login + admin flows.</summary>
public interface IUserLookup
{
    Task<ApplicationUser?> FindByEmailAsync(string email, CancellationToken ct = default);
    Task<ApplicationUser?> FindByIdAsync(Guid userId, CancellationToken ct = default);
}
