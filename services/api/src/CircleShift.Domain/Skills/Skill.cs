using System.Text.RegularExpressions;

namespace CircleShift.Domain.Skills;

/// <summary>
/// An entry in the canonical skill catalog. Offers and requests point at a
/// catalog skill instead of free text, so matching compares like with like.
/// </summary>
public sealed partial class Skill
{
    public const int SlugMaxLength = 64;
    public const int NameMaxLength = 80;

    /// <summary>Lowercase words separated by single hyphens, e.g. <c>bike-repair</c>.</summary>
    public const string SlugPattern = "^[a-z0-9]+(-[a-z0-9]+)*$";

    private Skill()
    {
    }

    public Guid Id { get; private set; }

    public string Slug { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public SkillCategory Category { get; private set; }

    public static Skill Create(Guid id, string slug, string name, SkillCategory category)
    {
        if (id == Guid.Empty)
        {
            throw new DomainException("skill.id_required", "A skill id is required.");
        }

        if (string.IsNullOrEmpty(slug) || slug.Length > SlugMaxLength || !SlugRegex().IsMatch(slug))
        {
            throw new DomainException("skill.slug_invalid", "Skill slugs use lowercase letters, digits and single hyphens.");
        }

        var trimmedName = name?.Trim() ?? string.Empty;
        if (trimmedName.Length is 0 or > NameMaxLength)
        {
            throw new DomainException("skill.name_length", $"Skill names must be 1-{NameMaxLength} characters.");
        }

        if (!Enum.IsDefined(category))
        {
            throw new DomainException("skill.category_invalid", "Unknown skill category.");
        }

        return new Skill { Id = id, Slug = slug, Name = trimmedName, Category = category };
    }

    [GeneratedRegex(SlugPattern, RegexOptions.CultureInvariant)]
    private static partial Regex SlugRegex();
}
