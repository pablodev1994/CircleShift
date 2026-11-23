namespace CircleShift.Domain.Members;

/// <summary>
/// A community member. CircleShift is adults-only, so a member cannot exist
/// without a recorded adult (18+) confirmation. Profiles, visibility and roles
/// are added in the accounts work; this is the minimal identity record.
/// </summary>
public sealed class Member
{
    public const int DisplayNameMinLength = 2;
    public const int DisplayNameMaxLength = 80;
    public const int TimeZoneIdMaxLength = 64;

    private Member()
    {
    }

    public Guid Id { get; private set; }

    public string DisplayName { get; private set; } = string.Empty;

    /// <summary>IANA time zone, for example <c>Europe/Madrid</c>.</summary>
    public string TimeZoneId { get; private set; } = string.Empty;

    public DateTimeOffset AdultConfirmedAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public static Member Register(Guid id, string displayName, string timeZoneId, bool confirmedAdult, DateTimeOffset now)
    {
        if (id == Guid.Empty)
        {
            throw new DomainException("member.id_required", "A member id is required.");
        }

        if (!confirmedAdult)
        {
            throw new DomainException("member.adult_confirmation_required", "Members must confirm they are 18 or older.");
        }

        return new Member
        {
            Id = id,
            DisplayName = NormalizeDisplayName(displayName),
            TimeZoneId = TimeZones.RequireIana(timeZoneId),
            AdultConfirmedAt = now.ToUniversalTime(),
            CreatedAt = now.ToUniversalTime(),
        };
    }

    public void Rename(string displayName) => DisplayName = NormalizeDisplayName(displayName);

    public void MoveToTimeZone(string timeZoneId) => TimeZoneId = TimeZones.RequireIana(timeZoneId);

    private static string NormalizeDisplayName(string? value)
    {
        var trimmed = value?.Trim() ?? string.Empty;
        if (trimmed.Length is < DisplayNameMinLength or > DisplayNameMaxLength)
        {
            throw new DomainException(
                "member.display_name_length",
                $"Display names must be {DisplayNameMinLength}-{DisplayNameMaxLength} characters.");
        }

        if (trimmed.Any(char.IsControl))
        {
            throw new DomainException("member.display_name_invalid", "Display names cannot contain control characters.");
        }

        return trimmed;
    }
}
