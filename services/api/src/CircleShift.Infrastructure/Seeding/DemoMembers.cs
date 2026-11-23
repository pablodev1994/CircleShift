using CircleShift.Domain.Members;

namespace CircleShift.Infrastructure.Seeding;

/// <summary>
/// Fictional adult demo members, matching the personas in
/// <c>docs/product/personas.md</c> plus a few extra members so matching has
/// more than one candidate. No real people, contact details or locations.
/// </summary>
public static class DemoMembers
{
    public const string IdKind = "demo-member";

    /// <summary>
    /// Handles are only used to derive stable ids; profiles, roles and offers
    /// arrive with the accounts and posts work.
    /// </summary>
    public static IReadOnlyList<DemoMember> All { get; } =
    [
        new("laura", "Laura M.", "America/Bogota", "Member persona: front-end developer, wants photography help"),
        new("tomas", "Tomás R.", "Europe/Madrid", "Member persona: photographer and cyclist, wants English practice"),
        new("marisol", "Marisol V.", "America/Mexico_City", "Coordinator persona: runs a language circle"),
        new("andres", "Andrés P.", "America/Lima", "Moderator persona: volunteer moderator"),
        new("camila", "Camila S.", "Europe/Lisbon", "Admin persona: manages policy and the skill catalog"),
        new("priya", "Priya K.", "Asia/Kolkata", "Member: Python tutor, wants watercolor tips"),
        new("jonas", "Jonas W.", "Europe/Berlin", "Member: bike mechanic, wants Spanish conversation"),
        new("amara", "Amara O.", "Africa/Lagos", "Member: interview coach, wants guitar basics"),
    ];

    public static IEnumerable<Member> Register(DateTimeOffset now) =>
        All.Select(member => Member.Register(member.Id, member.DisplayName, member.TimeZoneId, confirmedAdult: true, now));
}

public sealed record DemoMember(string Handle, string DisplayName, string TimeZoneId, string Note)
{
    public Guid Id => SeedIds.For(DemoMembers.IdKind, Handle);
}
