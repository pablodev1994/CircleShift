namespace CircleShift.Domain.Members;

/// <summary>
/// A community member and their profile. CircleShift is adults-only, so a
/// member cannot exist without a recorded adult (18+) confirmation. Profiles
/// are opt-in: a new member is <see cref="ProfileVisibility.Hidden"/> until
/// they choose otherwise. Elevated roles live in <see cref="RoleGrant"/>.
/// </summary>
public sealed class Member
{
    public const int DisplayNameMinLength = 2;
    public const int DisplayNameMaxLength = 80;
    public const int TimeZoneIdMaxLength = 64;
    public const int BioMaxLength = 500;
    public const int AreaLabelMaxLength = 60;

    private Member()
    {
    }

    public Guid Id { get; private set; }

    public string DisplayName { get; private set; } = string.Empty;

    /// <summary>IANA time zone, for example <c>Europe/Madrid</c>.</summary>
    public string TimeZoneId { get; private set; } = string.Empty;

    public DateTimeOffset AdultConfirmedAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Free text shown on the profile; line breaks allowed.</summary>
    public string? Bio { get; private set; }

    /// <summary>Normalized BCP 47 tags, in the member's order of preference.</summary>
    public List<string> Languages { get; private set; } = [];

    /// <summary>
    /// Optional coarse area such as "Bogotá" or "Online only". CircleShift never
    /// stores precise locations.
    /// </summary>
    public string? AreaLabel { get; private set; }

    public ProfileVisibility Visibility { get; private set; } = ProfileVisibility.Hidden;

    public DateTimeOffset UpdatedAt { get; private set; }

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
            UpdatedAt = now.ToUniversalTime(),
        };
    }

    public void Rename(string displayName) => DisplayName = NormalizeDisplayName(displayName);

    public void MoveToTimeZone(string timeZoneId) => TimeZoneId = TimeZones.RequireIana(timeZoneId);

    /// <summary>Replaces the editable profile fields in one step, validating all of them first.</summary>
    public void UpdateProfile(
        string displayName,
        string timeZoneId,
        string? bio,
        IEnumerable<string>? languages,
        string? areaLabel,
        ProfileVisibility visibility,
        DateTimeOffset now)
    {
        var name = NormalizeDisplayName(displayName);
        var zone = TimeZones.RequireIana(timeZoneId);
        var normalizedBio = NormalizeBio(bio);
        var normalizedLanguages = LanguageTags.Normalize(languages);
        var area = NormalizeAreaLabel(areaLabel);
        if (!Enum.IsDefined(visibility))
        {
            throw new DomainException("profile.visibility_invalid", "Choose hidden, members or public.");
        }

        DisplayName = name;
        TimeZoneId = zone;
        Bio = normalizedBio;
        Languages = [.. normalizedLanguages];
        AreaLabel = area;
        Visibility = visibility;
        UpdatedAt = now.ToUniversalTime();
    }

    private static string? NormalizeBio(string? value)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }

        if (trimmed.Length > BioMaxLength)
        {
            throw new DomainException("profile.bio_length", $"Bios can be up to {BioMaxLength} characters.");
        }

        if (trimmed.Any(character => char.IsControl(character) && character is not '\n' and not '\r'))
        {
            throw new DomainException("profile.bio_invalid", "Bios cannot contain control characters.");
        }

        return trimmed.ReplaceLineEndings("\n");
    }

    private static string? NormalizeAreaLabel(string? value)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }

        if (trimmed.Length > AreaLabelMaxLength || trimmed.Any(char.IsControl))
        {
            throw new DomainException(
                "profile.area_label_invalid",
                $"Area labels are single-line text up to {AreaLabelMaxLength} characters.");
        }

        return trimmed;
    }

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
