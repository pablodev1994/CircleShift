namespace CircleShift.Domain.Members;

/// <summary>
/// Availability and session times are stored per IANA zone so daylight saving
/// changes are handled by the time zone database, not by fixed offsets.
/// </summary>
public static class TimeZones
{
    public static string RequireIana(string? timeZoneId)
    {
        var value = timeZoneId?.Trim() ?? string.Empty;
        if (value.Length is 0 or > Member.TimeZoneIdMaxLength
            || !TimeZoneInfo.TryFindSystemTimeZoneById(value, out var zone)
            || !zone.HasIanaId)
        {
            throw new DomainException("time_zone.invalid", "Use an IANA time zone such as Europe/Madrid.");
        }

        return value;
    }
}
