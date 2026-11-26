using CircleShift.Application.Abstractions;
using CircleShift.Domain;
using CircleShift.Domain.Members;
using CircleShift.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace CircleShift.Infrastructure.Accounts;

/// <summary>
/// Requesting, granting, verifying and revoking elevated roles. Callers check
/// the caller's permission (admin-only actions) before calling in.
/// </summary>
public sealed class RoleGrantService(CircleShiftDbContext db, IClock clock, ILogger<RoleGrantService> logger)
{
    public Task<List<RoleGrant>> ListForMemberAsync(Guid memberId, CancellationToken cancellationToken = default) =>
        db.RoleGrants
            .AsNoTracking()
            .Where(grant => grant.MemberId == memberId)
            .OrderBy(grant => grant.RequestedAt)
            .ThenBy(grant => grant.Id)
            .ToListAsync(cancellationToken);

    public async Task<RoleGrant> RequestAsync(Guid memberId, CommunityRole role, string? reason, CancellationToken cancellationToken = default)
    {
        var now = clock.UtcNow;
        var grant = RoleGrant.Request(Guid.CreateVersion7(now), memberId, role, reason, now);
        await AddOpenGrantAsync(grant, cancellationToken);
        logger.LogInformation("Member {MemberId} requested role {Role} ({GrantId})", memberId, role.ToName(), grant.Id);
        return grant;
    }

    public async Task<RoleGrant> GrantAsync(Guid adminId, Guid memberId, CommunityRole role, CancellationToken cancellationToken = default)
    {
        if (!await db.Members.AnyAsync(member => member.Id == memberId, cancellationToken))
        {
            throw MemberNotFound();
        }

        if (adminId == memberId)
        {
            throw new DomainException("role.self_verification", "Members cannot verify their own role.");
        }

        var now = clock.UtcNow;
        var grant = RoleGrant.GrantedBy(Guid.CreateVersion7(now), memberId, role, adminId, now);
        await AddOpenGrantAsync(grant, cancellationToken);
        logger.LogInformation("Admin {AdminId} granted role {Role} to member {MemberId} ({GrantId})", adminId, role.ToName(), memberId, grant.Id);
        return grant;
    }

    public async Task<RoleGrant?> VerifyAsync(Guid adminId, Guid grantId, CancellationToken cancellationToken = default)
    {
        var grant = await db.RoleGrants.SingleOrDefaultAsync(candidate => candidate.Id == grantId, cancellationToken);
        if (grant is null)
        {
            return null;
        }

        grant.Verify(adminId, clock.UtcNow);
        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Admin {AdminId} verified role grant {GrantId}", adminId, grantId);
        return grant;
    }

    public async Task<RoleGrant?> RevokeAsync(Guid adminId, Guid grantId, CancellationToken cancellationToken = default)
    {
        var grant = await db.RoleGrants.SingleOrDefaultAsync(candidate => candidate.Id == grantId, cancellationToken);
        if (grant is null)
        {
            return null;
        }

        if (grant.Role == CommunityRole.Admin && grant.IsEffective && !await HasOtherAdminAsync(grant.MemberId, cancellationToken))
        {
            throw new DomainException("role.last_admin", "The last verified admin cannot be revoked.");
        }

        grant.Revoke(adminId, clock.UtcNow);
        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Admin {AdminId} revoked role grant {GrantId}", adminId, grantId);
        return grant;
    }

    /// <summary>
    /// Makes the given member the first admin. Refuses once any verified admin
    /// exists, so it cannot be used to add admins behind other admins' backs.
    /// </summary>
    public async Task<RoleGrant> BootstrapFirstAdminAsync(Guid memberId, CancellationToken cancellationToken = default)
    {
        if (await db.RoleGrants.AnyAsync(grant => grant.Role == CommunityRole.Admin && grant.VerifiedAt != null && grant.RevokedAt == null, cancellationToken))
        {
            throw new DomainException("role.admin_exists", "An admin already exists; ask them to grant the role.");
        }

        if (!await db.Members.AnyAsync(member => member.Id == memberId, cancellationToken))
        {
            throw MemberNotFound();
        }

        var now = clock.UtcNow;
        var grant = RoleGrant.SystemGranted(Guid.CreateVersion7(now), memberId, CommunityRole.Admin, now);
        await AddOpenGrantAsync(grant, cancellationToken);
        logger.LogInformation("Member {MemberId} bootstrapped as first admin ({GrantId})", memberId, grant.Id);
        return grant;
    }

    private Task<bool> HasOtherAdminAsync(Guid memberId, CancellationToken cancellationToken) =>
        db.RoleGrants.AnyAsync(
            grant => grant.Role == CommunityRole.Admin && grant.MemberId != memberId && grant.VerifiedAt != null && grant.RevokedAt == null,
            cancellationToken);

    private async Task AddOpenGrantAsync(RoleGrant grant, CancellationToken cancellationToken)
    {
        if (await db.RoleGrants.AnyAsync(open => open.MemberId == grant.MemberId && open.Role == grant.Role && open.RevokedAt == null, cancellationToken))
        {
            throw RoleAlreadyOpen();
        }

        db.RoleGrants.Add(grant);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException error) when (error.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw RoleAlreadyOpen();
        }
    }

    private static DomainException RoleAlreadyOpen() =>
        new("role.already_open", "This member already has or has requested this role.");

    private static DomainException MemberNotFound() =>
        new("member.not_found", "No member with this id.");
}
