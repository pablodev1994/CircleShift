using CircleShift.Domain.Members;
using CircleShift.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CircleShift.Infrastructure.Accounts;

/// <summary>
/// Loads what a signed-in member may do right now. Called on every request so
/// a revoked role or deleted member stops working without waiting for the
/// session cookie to expire.
/// </summary>
public sealed class MemberAccessService(CircleShiftDbContext db)
{
    public async Task<MemberAccess?> FindAsync(Guid memberId, CancellationToken cancellationToken = default)
    {
        var member = await db.Members
            .AsNoTracking()
            .Where(candidate => candidate.Id == memberId)
            .Select(candidate => new { candidate.Id, candidate.DisplayName, candidate.AdultConfirmedAt })
            .SingleOrDefaultAsync(cancellationToken);
        if (member is null)
        {
            return null;
        }

        // Unverified (requested) and revoked grants are filtered here; nothing
        // else in the app turns a role grant into a permission.
        var roles = await db.RoleGrants
            .AsNoTracking()
            .Where(grant => grant.MemberId == memberId && grant.VerifiedAt != null && grant.RevokedAt == null)
            .Select(grant => grant.Role)
            .ToListAsync(cancellationToken);

        return new MemberAccess(member.Id, member.DisplayName, member.AdultConfirmedAt, [.. roles.Distinct().Order()]);
    }
}

public sealed record MemberAccess(Guid MemberId, string DisplayName, DateTimeOffset AdultConfirmedAt, IReadOnlyList<CommunityRole> Roles);
