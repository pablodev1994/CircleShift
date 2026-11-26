namespace CircleShift.Domain.Members;

/// <summary>
/// One member blocking another. A block works both ways: neither member sees
/// the other's profile or appears in the other's directory and matches. Only
/// the blocker can lift it, and the blocked member is never told.
/// </summary>
public sealed class MemberBlock
{
    private MemberBlock()
    {
    }

    public Guid BlockerId { get; private set; }

    public Guid BlockedId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public static MemberBlock Create(Guid blockerId, Guid blockedId, DateTimeOffset now)
    {
        if (blockerId == Guid.Empty || blockedId == Guid.Empty)
        {
            throw new DomainException("block.member_required", "Both members are required.");
        }

        if (blockerId == blockedId)
        {
            throw new DomainException("block.self", "You cannot block yourself.");
        }

        return new MemberBlock { BlockerId = blockerId, BlockedId = blockedId, CreatedAt = now.ToUniversalTime() };
    }
}
