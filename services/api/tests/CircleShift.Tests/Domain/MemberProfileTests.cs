using CircleShift.Domain;
using CircleShift.Domain.Members;

namespace CircleShift.Tests.Domain;

public sealed class MemberProfileTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 26, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void New_members_start_hidden_with_an_empty_profile()
    {
        var member = NewMember();

        Assert.Equal(ProfileVisibility.Hidden, member.Visibility);
        Assert.Empty(member.Languages);
        Assert.Null(member.Bio);
        Assert.Null(member.AreaLabel);
        Assert.Equal(member.CreatedAt, member.UpdatedAt);
    }

    [Fact]
    public void Update_profile_normalizes_every_field()
    {
        var member = NewMember();

        member.UpdateProfile(
            " Laura M. ",
            "America/Bogota",
            "  Front-end developer.\r\nLearning photography.  ",
            ["ES", "en", "pt-br", "es"],
            "  Bogotá ",
            ProfileVisibility.Members,
            Now.AddHours(1));

        Assert.Equal("Laura M.", member.DisplayName);
        Assert.Equal("Front-end developer.\nLearning photography.", member.Bio);
        Assert.Equal(["es", "en", "pt-BR"], member.Languages);
        Assert.Equal("Bogotá", member.AreaLabel);
        Assert.Equal(ProfileVisibility.Members, member.Visibility);
        Assert.Equal(Now.AddHours(1), member.UpdatedAt);
    }

    [Fact]
    public void Invalid_update_changes_nothing()
    {
        var member = NewMember();

        var error = Assert.Throws<DomainException>(() => member.UpdateProfile(
            "New name", "Europe/Madrid", "bio", ["not a language"], null, ProfileVisibility.Public, Now.AddHours(1)));

        Assert.Equal("profile.language_invalid", error.Code);
        Assert.Equal("Laura", member.DisplayName);
        Assert.Equal("America/Bogota", member.TimeZoneId);
        Assert.Equal(ProfileVisibility.Hidden, member.Visibility);
    }

    [Fact]
    public void Undefined_visibility_is_rejected()
    {
        var member = NewMember();

        var error = Assert.Throws<DomainException>(() => member.UpdateProfile(
            "Laura", "America/Bogota", null, null, null, (ProfileVisibility)42, Now));

        Assert.Equal("profile.visibility_invalid", error.Code);
    }

    [Fact]
    public void Bio_limits_are_enforced()
    {
        var member = NewMember();

        Assert.Equal("profile.bio_length", Assert.Throws<DomainException>(() => member.UpdateProfile(
            "Laura", "America/Bogota", new string('a', Member.BioMaxLength + 1), null, null, ProfileVisibility.Hidden, Now)).Code);
        Assert.Equal("profile.bio_invalid", Assert.Throws<DomainException>(() => member.UpdateProfile(
            "Laura", "America/Bogota", "bell\u0007", null, null, ProfileVisibility.Hidden, Now)).Code);
    }

    [Fact]
    public void Area_label_must_be_short_and_single_line()
    {
        var member = NewMember();

        var error = Assert.Throws<DomainException>(() => member.UpdateProfile(
            "Laura", "America/Bogota", null, null, "Calle 1\nApto 2", ProfileVisibility.Hidden, Now));

        Assert.Equal("profile.area_label_invalid", error.Code);
    }

    [Theory]
    [InlineData("zh-hant-tw", "zh-Hant-TW")]
    [InlineData("es-419", "es-419")]
    [InlineData(" FIL ", "fil")]
    public void Language_tags_are_canonicalized(string input, string expected) =>
        Assert.Equal([expected], LanguageTags.Normalize([input]));

    [Theory]
    [InlineData("")]
    [InlineData("e")]
    [InlineData("english")]
    [InlineData("en_US")]
    [InlineData("en-US-x-private")]
    public void Malformed_language_tags_are_rejected(string input) =>
        Assert.Equal("profile.language_invalid", Assert.Throws<DomainException>(() => LanguageTags.Normalize([input])).Code);

    [Fact]
    public void At_most_eight_distinct_languages()
    {
        string[] nine = ["en", "es", "pt", "fr", "de", "it", "nl", "pl", "sv"];

        Assert.Equal("profile.too_many_languages", Assert.Throws<DomainException>(() => LanguageTags.Normalize(nine)).Code);
        Assert.Equal(8, LanguageTags.Normalize([.. nine[..8], "EN"]).Count);
    }

    private static Member NewMember() => Member.Register(Guid.NewGuid(), "Laura", "America/Bogota", confirmedAdult: true, Now);
}
