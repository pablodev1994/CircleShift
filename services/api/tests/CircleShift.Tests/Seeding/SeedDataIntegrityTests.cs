using CircleShift.Domain.Members;
using CircleShift.Domain.Skills;
using CircleShift.Infrastructure.Seeding;

namespace CircleShift.Tests.Seeding;

/// <summary>
/// The seed data is the first thing a new contributor sees, so it must obey the
/// same rules as data created through the app.
/// </summary>
public sealed class SeedDataIntegrityTests
{
    [Fact]
    public void Seed_ids_are_stable_rfc_version_5_guids()
    {
        var id = SeedIds.For("skill", "bike-repair");

        Assert.Equal(id, SeedIds.For("skill", "bike-repair"));
        Assert.NotEqual(id, SeedIds.For("demo-member", "bike-repair"));
        Assert.Equal(5, id.Version);
        Assert.Contains(id.ToString()[19], "89ab"); // RFC 9562 variant
    }

    [Fact]
    public void Catalog_ids_slugs_and_names_are_unique()
    {
        var catalog = SkillCatalog.Entries;

        Assert.Equal(catalog.Count, catalog.Select(skill => skill.Id).Distinct().Count());
        Assert.Equal(catalog.Count, catalog.Select(skill => skill.Slug).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(catalog.Count, catalog.Select(skill => skill.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void Catalog_covers_every_category()
    {
        var used = SkillCatalog.Entries.Select(skill => skill.Category).ToHashSet();

        Assert.All(Enum.GetValues<SkillCategory>(), category => Assert.Contains(category, used));
    }

    [Fact]
    public void Catalog_ids_derive_from_slugs()
    {
        Assert.All(SkillCatalog.Entries, skill =>
            Assert.Equal(SeedIds.For(SkillCatalog.IdKind, skill.Slug), skill.Id));
    }

    [Fact]
    public void Catalog_lookup_rejects_unknown_slugs()
    {
        Assert.Equal("Bike repair", SkillCatalog.Get("bike-repair").Name);
        Assert.Throws<KeyNotFoundException>(() => SkillCatalog.Get("unknown-skill"));
    }

    [Fact]
    public void Demo_members_are_unique_adults_in_iana_zones()
    {
        var now = new DateTimeOffset(2026, 11, 20, 12, 0, 0, TimeSpan.Zero);
        var members = DemoMembers.Register(now).ToList();

        Assert.Equal(DemoMembers.All.Count, members.Count);
        Assert.Equal(members.Count, members.Select(member => member.Id).Distinct().Count());
        Assert.Equal(members.Count, members.Select(member => member.DisplayName).Distinct().Count());
        Assert.All(members, member =>
        {
            Assert.Equal(now, member.AdultConfirmedAt);
            Assert.Equal(member.TimeZoneId, TimeZones.RequireIana(member.TimeZoneId));
        });
    }

    [Fact]
    public void Demo_members_include_every_persona()
    {
        var handles = DemoMembers.All.Select(member => member.Handle).ToHashSet();

        Assert.Subset(handles, new HashSet<string> { "laura", "tomas", "marisol", "andres", "camila" });
    }

    [Fact]
    public void Demo_members_hold_no_contact_details()
    {
        Assert.All(DemoMembers.All, member =>
        {
            Assert.DoesNotContain('@', member.DisplayName);
            Assert.DoesNotContain(member.DisplayName, char.IsDigit);
        });
    }
}
