using CircleShift.Application.Abstractions;
using CircleShift.Domain;
using CircleShift.Domain.Members;
using CircleShift.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace CircleShift.Infrastructure.Profiles;

/// <summary>
/// Profiles and blocks. Every read that can return someone else's profile goes
/// through <see cref="VisibleTo"/>, so visibility and blocks are applied in the
/// database query instead of being filtered out afterwards.
/// </summary>
public sealed class ProfileService(CircleShiftDbContext db, IClock clock, ILogger<ProfileService> logger)
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 50;
    public const int MaxSearchLength = Member.DisplayNameMaxLength;

    public Task<Member?> GetOwnAsync(Guid memberId, CancellationToken cancellationToken = default) =>
        db.Members.AsNoTracking().SingleOrDefaultAsync(member => member.Id == memberId, cancellationToken);

    /// <summary>Edits the caller's own profile. There is no way to name another member here.</summary>
    public async Task<Member> UpdateOwnAsync(Guid memberId, ProfileUpdate update, CancellationToken cancellationToken = default)
    {
        var member = await db.Members.SingleOrDefaultAsync(candidate => candidate.Id == memberId, cancellationToken)
            ?? throw new DomainException("member.not_found", "No member with this id.");

        member.UpdateProfile(
            update.DisplayName,
            update.TimeZoneId,
            update.Bio,
            update.Languages,
            update.AreaLabel,
            update.Visibility,
            clock.UtcNow);
        await db.SaveChangesAsync(cancellationToken);
        return member;
    }

    /// <summary>
    /// The profile as <paramref name="viewerId"/> may see it, or <c>null</c> when
    /// it does not exist, is hidden, needs a sign-in, or either member blocked
    /// the other. Callers answer all of those the same way.
    /// </summary>
    public Task<Member?> FindVisibleAsync(Guid? viewerId, Guid memberId, CancellationToken cancellationToken = default) =>
        VisibleTo(viewerId, includeSelf: true).SingleOrDefaultAsync(member => member.Id == memberId, cancellationToken);

    /// <summary>The member directory: visible profiles other than the viewer's, by display name.</summary>
    public async Task<DirectoryPage> SearchAsync(Guid? viewerId, DirectoryQuery query, CancellationToken cancellationToken = default)
    {
        var limit = query.Limit ?? DefaultPageSize;
        var offset = query.Offset ?? 0;
        if (limit is < 1 or > MaxPageSize || offset < 0)
        {
            throw new DomainException("directory.paging_invalid", $"Use a limit of 1-{MaxPageSize} and an offset of 0 or more.");
        }

        var members = VisibleTo(viewerId, includeSelf: false);

        var text = query.Text?.Trim();
        if (!string.IsNullOrEmpty(text))
        {
            if (text.Length > MaxSearchLength)
            {
                throw new DomainException("directory.query_too_long", $"Search text can be up to {MaxSearchLength} characters.");
            }

            // string.Contains is translated with LIKE wildcards escaped, so "%"
            // and "_" match themselves rather than everything.
            var lowered = text.ToLowerInvariant();
            members = members.Where(member => member.DisplayName.ToLower().Contains(lowered));
        }

        if (!string.IsNullOrWhiteSpace(query.Language))
        {
            var tag = LanguageTags.Normalize([query.Language]).Single();
            members = members.Where(member => member.Languages.Contains(tag));
        }

        var total = await members.CountAsync(cancellationToken);
        var page = await members
            .OrderBy(member => member.DisplayName)
            .ThenBy(member => member.Id)
            .Skip(offset)
            .Take(limit)
            .ToListAsync(cancellationToken);
        return new DirectoryPage(page, total, limit, offset);
    }

    public async Task<IReadOnlyList<BlockedMember>> ListBlocksAsync(Guid blockerId, CancellationToken cancellationToken = default) =>
        await (from block in db.MemberBlocks.AsNoTracking()
               join member in db.Members.AsNoTracking() on block.BlockedId equals member.Id
               where block.BlockerId == blockerId
               orderby block.CreatedAt descending, member.Id
               select new BlockedMember(member.Id, member.DisplayName, block.CreatedAt))
            .ToListAsync(cancellationToken);

    /// <summary>Blocks a member. Blocking someone already blocked changes nothing.</summary>
    public async Task BlockAsync(Guid blockerId, Guid blockedId, CancellationToken cancellationToken = default)
    {
        var block = MemberBlock.Create(blockerId, blockedId, clock.UtcNow);

        if (!await db.Members.AnyAsync(member => member.Id == blockedId, cancellationToken))
        {
            throw new DomainException("member.not_found", "No member with this id.");
        }

        if (await db.MemberBlocks.AnyAsync(existing => existing.BlockerId == blockerId && existing.BlockedId == blockedId, cancellationToken))
        {
            return;
        }

        db.MemberBlocks.Add(block);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException error) when (error.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // The same block was saved by a parallel request.
            db.ChangeTracker.Clear();
            return;
        }

        logger.LogInformation("Member {BlockerId} blocked member {BlockedId}", blockerId, blockedId);
    }

    /// <summary>Lifts a block. Lifting one that does not exist changes nothing.</summary>
    public async Task UnblockAsync(Guid blockerId, Guid blockedId, CancellationToken cancellationToken = default)
    {
        var block = await db.MemberBlocks.SingleOrDefaultAsync(
            existing => existing.BlockerId == blockerId && existing.BlockedId == blockedId,
            cancellationToken);
        if (block is null)
        {
            return;
        }

        db.MemberBlocks.Remove(block);
        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Member {BlockerId} unblocked member {BlockedId}", blockerId, blockedId);
    }

    /// <summary>
    /// Profiles <paramref name="viewerId"/> may see: visitors see public
    /// profiles; members also see members-only ones, except where either side
    /// blocked the other. Hidden profiles are only visible to their owner.
    /// </summary>
    private IQueryable<Member> VisibleTo(Guid? viewerId, bool includeSelf)
    {
        var members = db.Members.AsNoTracking();
        if (viewerId is not { } viewer)
        {
            return members.Where(member => member.Visibility == ProfileVisibility.Public);
        }

        return members.Where(member =>
            (includeSelf && member.Id == viewer)
            || (member.Id != viewer
                && member.Visibility != ProfileVisibility.Hidden
                && !db.MemberBlocks.Any(block =>
                    (block.BlockerId == viewer && block.BlockedId == member.Id)
                    || (block.BlockerId == member.Id && block.BlockedId == viewer))));
    }
}

public sealed record ProfileUpdate(
    string DisplayName,
    string TimeZoneId,
    string? Bio,
    IReadOnlyList<string>? Languages,
    string? AreaLabel,
    ProfileVisibility Visibility);

public sealed record DirectoryQuery(string? Text = null, string? Language = null, int? Limit = null, int? Offset = null);

public sealed record DirectoryPage(IReadOnlyList<Member> Items, int Total, int Limit, int Offset);

public sealed record BlockedMember(Guid MemberId, string DisplayName, DateTimeOffset BlockedAt);
