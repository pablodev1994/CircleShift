using CircleShift.Domain;
using CircleShift.Domain.Members;

namespace CircleShift.Tests.Domain;

public sealed class MemberBlockTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 26, 9, 0, 0, TimeSpan.FromHours(-5));

    [Fact]
    public void Block_records_both_members_and_the_time_in_utc()
    {
        var blocker = Guid.NewGuid();
        var blocked = Guid.NewGuid();

        var block = MemberBlock.Create(blocker, blocked, Now);

        Assert.Equal(blocker, block.BlockerId);
        Assert.Equal(blocked, block.BlockedId);
        Assert.Equal(TimeSpan.Zero, block.CreatedAt.Offset);
        Assert.Equal(Now, block.CreatedAt);
    }

    [Fact]
    public void Members_cannot_block_themselves()
    {
        var member = Guid.NewGuid();

        var error = Assert.Throws<DomainException>(() => MemberBlock.Create(member, member, Now));

        Assert.Equal("block.self", error.Code);
    }

    [Fact]
    public void Both_member_ids_are_required()
    {
        Assert.Equal("block.member_required", Assert.Throws<DomainException>(() => MemberBlock.Create(Guid.Empty, Guid.NewGuid(), Now)).Code);
        Assert.Equal("block.member_required", Assert.Throws<DomainException>(() => MemberBlock.Create(Guid.NewGuid(), Guid.Empty, Now)).Code);
    }

    [Theory]
    [InlineData("hidden", ProfileVisibility.Hidden)]
    [InlineData(" Members ", ProfileVisibility.Members)]
    [InlineData("PUBLIC", ProfileVisibility.Public)]
    public void Visibility_names_parse_case_insensitively_and_round_trip(string name, ProfileVisibility expected)
    {
        Assert.True(ProfileVisibilities.TryParse(name, out var visibility));
        Assert.Equal(expected, visibility);
        Assert.Equal(name.Trim().ToLowerInvariant(), visibility.ToName());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("friends")]
    public void Unknown_visibility_names_are_rejected(string? name) =>
        Assert.False(ProfileVisibilities.TryParse(name, out _));
}
