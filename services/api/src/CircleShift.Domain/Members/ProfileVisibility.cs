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
