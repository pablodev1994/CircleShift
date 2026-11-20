using CircleShift.Domain;
using CircleShift.Domain.Members;

namespace CircleShift.Tests.Domain;

public sealed class MemberTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 20, 16, 0, 0, TimeSpan.FromHours(1));

    [Fact]
    public void Register_trims_the_name_and_stores_times_in_utc()
    {
        var member = Member.Register(Guid.NewGuid(), "  Laura Gómez ", "Europe/Madrid", confirmedAdult: true, Now);

        Assert.Equal("Laura Gómez", member.DisplayName);
        Assert.Equal("Europe/Madrid", member.TimeZoneId);
        Assert.Equal(TimeSpan.Zero, member.CreatedAt.Offset);
        Assert.Equal(Now, member.AdultConfirmedAt);
    }

    [Fact]
    public void Register_rejects_members_without_adult_confirmation()
    {
        var error = Assert.Throws<DomainException>(() =>
            Member.Register(Guid.NewGuid(), "Tomás", "America/Bogota", confirmedAdult: false, Now));

        Assert.Equal("member.adult_confirmation_required", error.Code);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" A ")]
    [InlineData("Line\nbreak")]
    public void Register_rejects_invalid_display_names(string name)
    {
        var error = Assert.Throws<DomainException>(() =>
            Member.Register(Guid.NewGuid(), name, "Europe/Madrid", confirmedAdult: true, Now));

        Assert.StartsWith("member.display_name", error.Code, StringComparison.Ordinal);
    }

    [Fact]
    public void Register_rejects_display_names_over_80_characters()
    {
        var error = Assert.Throws<DomainException>(() =>
            Member.Register(Guid.NewGuid(), new string('a', 81), "Europe/Madrid", confirmedAdult: true, Now));

        Assert.Equal("member.display_name_length", error.Code);
    }

    [Theory]
    [InlineData("")]
    [InlineData("GMT+2")]
    [InlineData("Mars/Olympus_Mons")]
    public void Register_rejects_time_zones_that_are_not_iana(string timeZoneId)
    {
        var error = Assert.Throws<DomainException>(() =>
            Member.Register(Guid.NewGuid(), "Tomás", timeZoneId, confirmedAdult: true, Now));

        Assert.Equal("time_zone.invalid", error.Code);
    }

    [Fact]
    public void Register_rejects_an_empty_id()
    {
        var error = Assert.Throws<DomainException>(() =>
            Member.Register(Guid.Empty, "Tomás", "America/Bogota", confirmedAdult: true, Now));

        Assert.Equal("member.id_required", error.Code);
    }

    [Fact]
    public void MoveToTimeZone_validates_the_new_zone()
    {
        var member = Member.Register(Guid.NewGuid(), "Tomás", "America/Bogota", confirmedAdult: true, Now);

        member.MoveToTimeZone("Asia/Tokyo");
        Assert.Equal("Asia/Tokyo", member.TimeZoneId);

        Assert.Throws<DomainException>(() => member.MoveToTimeZone("Not/AZone"));
        Assert.Equal("Asia/Tokyo", member.TimeZoneId);
    }
}
