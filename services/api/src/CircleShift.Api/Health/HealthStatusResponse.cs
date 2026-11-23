namespace CircleShift.Api.Health;

/// <summary>
/// Overall status plus one status per dependency. Exception messages are never
/// included, so hostnames and connection strings cannot leak to callers.
/// </summary>
public sealed record HealthStatusResponse(string Status, string Version, IReadOnlyDictionary<string, string> Checks);
