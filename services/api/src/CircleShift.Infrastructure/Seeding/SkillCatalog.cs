using CircleShift.Domain.Skills;

namespace CircleShift.Infrastructure.Seeding;

/// <summary>
/// The canonical skill catalog every environment starts with. Admins extend it
/// later; entries here are never renamed in place, because offers, requests and
/// match explanations refer to them by slug.
/// </summary>
public static class SkillCatalog
{
    public const string IdKind = "skill";

    private static readonly (string Slug, string Name, SkillCategory Category)[] Definitions =
    [
        ("english-conversation", "English conversation", SkillCategory.Languages),
        ("spanish-conversation", "Spanish conversation", SkillCategory.Languages),
        ("portuguese-basics", "Portuguese basics", SkillCategory.Languages),
        ("german-basics", "German basics", SkillCategory.Languages),
        ("react", "React", SkillCategory.Technology),
        ("typescript", "TypeScript", SkillCategory.Technology),
        ("python-basics", "Python basics", SkillCategory.Technology),
        ("spreadsheets", "Spreadsheets", SkillCategory.Technology),
        ("portrait-photography", "Portrait photography", SkillCategory.Creative),
        ("photo-editing", "Photo editing", SkillCategory.Creative),
        ("guitar-basics", "Guitar basics", SkillCategory.Creative),
        ("watercolor-painting", "Watercolor painting", SkillCategory.Creative),
        ("bike-repair", "Bike repair", SkillCategory.Home),
        ("gardening", "Gardening", SkillCategory.Home),
        ("home-cooking", "Home cooking", SkillCategory.Home),
        ("sewing-repairs", "Sewing repairs", SkillCategory.Home),
        ("resume-review", "Résumé review", SkillCategory.Career),
        ("interview-practice", "Interview practice", SkillCategory.Career),
        ("public-speaking", "Public speaking", SkillCategory.Career),
        ("personal-budgeting", "Personal budgeting", SkillCategory.Career),
        ("yoga-basics", "Yoga basics", SkillCategory.Wellbeing),
        ("running-coaching", "Running coaching", SkillCategory.Wellbeing),
        ("meditation", "Meditation", SkillCategory.Wellbeing),
        ("meal-planning", "Meal planning", SkillCategory.Wellbeing),
    ];

    /// <summary>New entity instances on every call, so separate seed runs never share tracked objects.</summary>
    public static IReadOnlyList<Skill> Entries => Definitions
        .Select(entry => Skill.Create(SeedIds.For(IdKind, entry.Slug), entry.Slug, entry.Name, entry.Category))
        .ToArray();

    public static Skill Get(string slug) =>
        Entries.SingleOrDefault(skill => skill.Slug == slug)
        ?? throw new KeyNotFoundException($"'{slug}' is not in the canonical skill catalog.");
}
