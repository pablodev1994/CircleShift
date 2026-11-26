namespace CircleShift.Domain.Members;

/// <summary>
/// Who can see a member's profile. Profiles are opt-in, so new members start
/// <see cref="Hidden"/> and choose to appear in the directory and matching.
/// </summary>
public enum ProfileVisibility
{
    /// <summary>Only the member (and moderators handling a report) can see it.</summary>
    Hidden,

    /// <summary>Signed-in members can see it.</summary>
    Members,

    /// <summary>Anyone, including visitors who are not signed in.</summary>
    Public,
}

/// <summary>The lower-case names used in the API and the database.</summary>
public static class ProfileVisibilities
{
    public static string ToName(this ProfileVisibility visibility) => visibility switch
    {
        ProfileVisibility.Hidden => "hidden",
        ProfileVisibility.Members => "members",
        ProfileVisibility.Public => "public",
        _ => throw new ArgumentOutOfRangeException(nameof(visibility), visibility, "Unknown profile visibility."),
    };

    public static bool TryParse(string? name, out ProfileVisibility visibility)
    {
        switch (name?.Trim().ToLowerInvariant())
        {
            case "hidden":
                visibility = ProfileVisibility.Hidden;
                return true;
            case "members":
                visibility = ProfileVisibility.Members;
                return true;
            case "public":
                visibility = ProfileVisibility.Public;
                return true;
            default:
                visibility = default;
                return false;
        }
    }
}
