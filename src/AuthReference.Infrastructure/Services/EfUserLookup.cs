using AuthReference.Domain.Entities;
using AuthReference.Domain.Services;
using AuthReference.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AuthReference.Infrastructure.Services;

/// <summary>
/// Read-side of the user store. Returns tracked entities so the caller may
/// mutate them (e.g. <see cref="EfUserActivityRecorder"/> updates
/// <c>LastLoginAtUtc</c>); callers that only want to read should call
/// <c>AsNoTracking()</c> explicitly via the DbContext if they need it.
/// </summary>
public sealed class EfUserLookup : IUserLookup
{
    private readonly AppDbContext _db;
    public EfUserLookup(AppDbContext db) => _db = db;

    public Task<ApplicationUser?> FindByEmailAsync(string email, CancellationToken ct = default) =>
        _db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);

    public Task<ApplicationUser?> FindByIdAsync(Guid userId, CancellationToken ct = default) =>
        _db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
}
