namespace CircleShift.Application.Abstractions;

/// <summary>
/// Current time as a port, so expiry, availability and settlement rules can be
/// tested with fixed or shifted clocks (including daylight saving changes).
/// </summary>
public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
