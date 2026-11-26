using System.Text.RegularExpressions;

namespace CircleShift.Domain.Members;

/// <summary>
/// Languages a member can exchange skills in, as simple BCP 47 tags
/// (<c>es</c>, <c>pt-BR</c>, <c>zh-Hant</c>). Tags are normalized to their
/// canonical case so <c>PT-br</c> and <c>pt-BR</c> are the same language.
/// </summary>
public static partial class LanguageTags
{
    public const int MaxPerMember = 8;
    public const int TagMaxLength = 16;

    public static IReadOnlyList<string> Normalize(IEnumerable<string>? tags)
    {
        var result = new List<string>();
        foreach (var raw in tags ?? [])
        {
            var tag = NormalizeOne(raw);
            if (!result.Contains(tag, StringComparer.Ordinal))
            {
                result.Add(tag);
            }
        }

        if (result.Count > MaxPerMember)
        {
            throw new DomainException("profile.too_many_languages", $"List at most {MaxPerMember} languages.");
        }

        return result;
    }

    private static string NormalizeOne(string? raw)
    {
        var value = raw?.Trim() ?? string.Empty;
        var match = TagPattern().Match(value);
        if (value.Length > TagMaxLength || !match.Success)
        {
            throw new DomainException("profile.language_invalid", "Use language tags such as en, es or pt-BR.");
        }

        var tag = match.Groups["language"].Value.ToLowerInvariant();
        if (match.Groups["script"].Success)
        {
            var script = match.Groups["script"].Value;
            tag += "-" + char.ToUpperInvariant(script[0]) + script[1..].ToLowerInvariant();
        }

        if (match.Groups["region"].Success)
        {
            tag += "-" + match.Groups["region"].Value.ToUpperInvariant();
        }

        return tag;
    }

    [GeneratedRegex(@"^(?<language>[A-Za-z]{2,3})(-(?<script>[A-Za-z]{4}))?(-(?<region>[A-Za-z]{2}|[0-9]{3}))?$", RegexOptions.CultureInvariant)]
    private static partial Regex TagPattern();
}
