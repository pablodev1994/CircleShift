using CircleShift.Domain;
using CircleShift.Domain.Members;

namespace CircleShift.Tests.Domain;

public sealed class RoleGrantTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 26, 9, 0, 0, TimeSpan.Zero);
    private static readonly Guid Member = Guid.NewGuid();
    private static readonly Guid Admin = Guid.NewGuid();

    [Fact]
    public void Requested_grant_is_not_effective_until_verified()
    {
        var grant = RoleGrant.Request(Guid.NewGuid(), Member, CommunityRole.Coordinator, "I run the Spanish circle", Now);

        Assert.False(grant.IsEffective);

        grant.Verify(Admin, Now.AddMinutes(5));

        Assert.True(grant.IsEffective);
        Assert.Equal(Admin, grant.VerifiedBy);
    }

    [Fact]
    public void Members_cannot_verify_their_own_grant()
    {
        var grant = RoleGrant.Request(Guid.NewGuid(), Admin, CommunityRole.Moderator, null, Now);

        var error = Assert.Throws<DomainException>(() => grant.Verify(Admin, Now));

        Assert.Equal("role.self_verification", error.Code);
        Assert.False(grant.IsEffective);
    }

    [Fact]
    public void Revoked_grant_stops_counting_and_cannot_be_verified_again()
    {
        var grant = RoleGrant.GrantedBy(Guid.NewGuid(), Member, CommunityRole.Moderator, Admin, Now);

        grant.Revoke(Admin, Now.AddDays(1));

        Assert.False(grant.IsEffective);
        Assert.Equal("role.revoked", Assert.Throws<DomainException>(() => grant.Verify(Guid.NewGuid(), Now)).Code);
        Assert.Equal("role.revoked", Assert.Throws<DomainException>(() => grant.Revoke(Admin, Now)).Code);
    }

    [Fact]
    public void Verifying_twice_is_refused()
    {
        var grant = RoleGrant.GrantedBy(Guid.NewGuid(), Member, CommunityRole.Coordinator, Admin, Now);

        Assert.Equal("role.already_verified", Assert.Throws<DomainException>(() => grant.Verify(Guid.NewGuid(), Now)).Code);
    }

    [Fact]
    public void System_grant_is_verified_without_a_verifier()
    {
        var grant = RoleGrant.SystemGranted(Guid.NewGuid(), Member, CommunityRole.Admin, Now);

        Assert.True(grant.IsEffective);
        Assert.Null(grant.VerifiedBy);
        Assert.Equal(Now, grant.VerifiedAt);
    }

    [Theory]
    [InlineData("line\nbreak")]
    [InlineData("tab\tinside")]
    public void Reasons_must_be_single_line(string reason)
    {
        var error = Assert.Throws<DomainException>(() => RoleGrant.Request(Guid.NewGuid(), Member, CommunityRole.Coordinator, reason, Now));

        Assert.Equal("role.reason_invalid", error.Code);
    }

    [Theory]
    [InlineData("coordinator", CommunityRole.Coordinator)]
    [InlineData("moderator", CommunityRole.Moderator)]
    [InlineData("admin", CommunityRole.Admin)]
    public void Role_names_round_trip(string name, CommunityRole role)
    {
        Assert.True(CommunityRoles.TryParse(name, out var parsed));
        Assert.Equal(role, parsed);
        Assert.Equal(name, role.ToName());
    }

    [Theory]
    [InlineData("Admin")]
    [InlineData("member")]
    [InlineData("superuser")]
    [InlineData(null)]
    public void Unknown_role_names_are_rejected(string? name) => Assert.False(CommunityRoles.TryParse(name, out _));
}
