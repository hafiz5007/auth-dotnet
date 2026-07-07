using AuthReference.Domain.Entities;
using AuthReference.Domain.Services;
using AuthReference.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AuthReference.Infrastructure.Services;

/// <summary>
/// Writes login timestamps + audit rows. Deliberately uses ExecuteUpdate so
/// the login handler does not need to load the user entity again just to
/// update a single column — a small hot-path optimisation.
/// </summary>
public sealed class EfUserActivityRecorder : IUserActivityRecorder
{
    private readonly AppDbContext _db;
    private readonly IClock _clock;

    public EfUserActivityRecorder(AppDbContext db, IClock clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task RecordLoginAsync(Guid userId, DateTimeOffset whenUtc, string? ipAddress, CancellationToken ct = default)
    {
        await _db.Users
            .Where(u => u.Id == userId)
            .ExecuteUpdateAsync(setter =>
                setter.SetProperty(u => u.LastLoginAtUtc, whenUtc), ct);

        _db.AuditEvents.Add(new AuthAuditEvent
        {
            UserId = userId,
            EventType = "LOGIN_OK",
            IpAddress = ipAddress,
            OccurredAtUtc = _clock.UtcNow
        });
        await _db.SaveChangesAsync(ct);
    }
}
