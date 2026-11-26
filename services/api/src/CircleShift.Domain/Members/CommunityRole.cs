namespace CircleShift.Domain.Members;

/// <summary>
/// Elevated roles from <c>docs/product/personas.md</c>. Every member has the
/// baseline member capabilities; these roles add to them and only count once
/// a <see cref="RoleGrant"/> has been verified.
/// </summary>
public enum CommunityRole
{
    Coordinator,
    Moderator,
    Admin,
}

public static class CommunityRoles
{
    /// <summary>Lower-case names used in claims, the API and the database.</summary>
    public static string ToName(this CommunityRole role) => role switch
    {
        CommunityRole.Coordinator => "coordinator",
        CommunityRole.Moderator => "moderator",
        CommunityRole.Admin => "admin",
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, null),
    };

    public static bool TryParse(string? value, out CommunityRole role)
    {
        foreach (var candidate in Enum.GetValues<CommunityRole>())
        {
            if (string.Equals(candidate.ToName(), value, StringComparison.Ordinal))
            {
                role = candidate;
                return true;
            }
        }

        role = default;
        return false;
    }
}
