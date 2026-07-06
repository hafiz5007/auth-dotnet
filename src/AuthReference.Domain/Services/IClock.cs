namespace AuthReference.Domain.Services;

/// <summary>
/// Injected clock. Every service that needs "now" takes an <see cref="IClock"/>
/// rather than calling <c>DateTimeOffset.UtcNow</c> directly. Makes token-expiry
/// tests deterministic and lets us fast-forward in integration tests.
/// </summary>
public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
