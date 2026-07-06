namespace AuthReference.Application.Abstractions;

/// <summary>
/// Per-request contextual data that handlers may need without dragging in
/// <c>HttpContext</c>. Populated by an API-layer middleware in Phase 4.
/// Kept as an interface here so tests can supply a static implementation.
/// </summary>
public interface IRequestContext
{
    string? IpAddress { get; }
    string? UserAgent { get; }
    string CorrelationId { get; }
}
