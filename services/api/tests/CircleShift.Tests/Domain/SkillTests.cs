using CircleShift.Domain;
using CircleShift.Domain.Skills;

namespace CircleShift.Tests.Domain;

public sealed class SkillTests
{
    [Theory]
    [InlineData("bike-repair")]
    [InlineData("english-conversation")]
    [InlineData("react")]
    [InlineData("3d-printing")]
    public void Create_accepts_canonical_slugs(string slug)
    {
        var skill = Skill.Create(Guid.NewGuid(), slug, " Some skill ", SkillCategory.Home);

        Assert.Equal(slug, skill.Slug);
        Assert.Equal("Some skill", skill.Name);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Bike-Repair")]
    [InlineData("bike--repair")]
    [InlineData("-bike")]
    [InlineData("bike repair")]
    [InlineData("bike_repair")]
    [InlineData("%")]
    public void Create_rejects_non_canonical_slugs(string slug)
    {
        var error = Assert.Throws<DomainException>(() =>
            Skill.Create(Guid.NewGuid(), slug, "Bike repair", SkillCategory.Home));

        Assert.Equal("skill.slug_invalid", error.Code);
    }

    [Fact]
    public void Create_rejects_slugs_over_64_characters()
    {
        var error = Assert.Throws<DomainException>(() =>
            Skill.Create(Guid.NewGuid(), new string('a', 65), "Long", SkillCategory.Career));

        Assert.Equal("skill.slug_invalid", error.Code);
    }

    [Fact]
    public void Create_rejects_blank_names_and_unknown_categories()
    {
        Assert.Equal("skill.name_length", Assert.Throws<DomainException>(() =>
            Skill.Create(Guid.NewGuid(), "gardening", "   ", SkillCategory.Home)).Code);

        Assert.Equal("skill.category_invalid", Assert.Throws<DomainException>(() =>
            Skill.Create(Guid.NewGuid(), "gardening", "Gardening", (SkillCategory)99)).Code);
    }
}
