namespace AuthReference.Domain.Services;

/// <summary>
/// Fire-and-forget write path for lightweight user-activity signals — currently
/// just "last successful login". Kept off the read <see cref="IUserLookup"/> so
/// the login handler does not need to know about the user store's write model.
/// </summary>
public interface IUserActivityRecorder
{
    Task RecordLoginAsync(Guid userId, DateTimeOffset whenUtc, string? ipAddress, CancellationToken ct = default);
}
