using CircleShift.Application.Abstractions;
using CircleShift.Domain.Accounts;
using CircleShift.Domain.Members;
using CircleShift.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CircleShift.Infrastructure.Seeding;

/// <summary>
/// Inserts the canonical skill catalog and, when asked, the fictional demo
/// members. Safe to run repeatedly: rows that already exist are left alone,
/// and a catalog slug owned by a different id stops the run instead of
/// creating a second skill with the same meaning. Demo members get their
/// persona roles as verified seed grants, and sign-in accounts only when a
/// demo password is supplied (it is never stored in the repository).
/// </summary>
public sealed class DemoDataSeeder(
    CircleShiftDbContext db,
    IClock clock,
    IPasswordHasher<MemberAccount> hasher,
    ILogger<DemoDataSeeder> logger)
{
    public async Task<SeedResult> SeedAsync(bool includeDemoMembers, string? demoPassword = null, CancellationToken cancellationToken = default)
    {
        if (demoPassword is not null)
        {
            MemberAccount.RequireAcceptablePassword(demoPassword);
        }

        var existingSkills = await db.Skills
            .AsNoTracking()
            .Select(skill => new { skill.Id, skill.Slug })
            .ToListAsync(cancellationToken);
        var idsBySlug = existingSkills.ToDictionary(skill => skill.Slug, skill => skill.Id, StringComparer.Ordinal);

        var skillsAdded = 0;
        foreach (var skill in SkillCatalog.Entries)
        {
            if (idsBySlug.TryGetValue(skill.Slug, out var existingId))
            {
                if (existingId != skill.Id)
                {
                    throw new SeedConflictException(
                        $"Skill slug '{skill.Slug}' already belongs to {existingId}, not the catalog id {skill.Id}.");
                }

                continue;
            }

            db.Skills.Add(skill);
            skillsAdded++;
        }

        var membersAdded = 0;
        var rolesAdded = 0;
        var accountsAdded = 0;
        if (includeDemoMembers)
        {
            var demoIds = DemoMembers.All.Select(member => member.Id).ToList();
            var existingMemberIds = await db.Members
                .AsNoTracking()
                .Where(member => demoIds.Contains(member.Id))
                .Select(member => member.Id)
                .ToListAsync(cancellationToken);

            foreach (var member in DemoMembers.Register(clock.UtcNow).Where(member => !existingMemberIds.Contains(member.Id)))
            {
                db.Members.Add(member);
                membersAdded++;
            }

            rolesAdded = await AddDemoRolesAsync(cancellationToken);
            if (demoPassword is not null)
            {
                accountsAdded = await AddDemoAccountsAsync(demoPassword, cancellationToken);
            }
        }

        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Seed finished: {SkillsAdded} skills added ({SkillTotal} in catalog), {MembersAdded} demo members, {RolesAdded} demo roles, {AccountsAdded} demo accounts added",
            skillsAdded,
            SkillCatalog.Entries.Count,
            membersAdded,
            rolesAdded,
            accountsAdded);

        return new SeedResult(skillsAdded, membersAdded, rolesAdded, accountsAdded);
    }

    private async Task<int> AddDemoRolesAsync(CancellationToken cancellationToken)
    {
        var withRoles = DemoMembers.All.Where(member => member.Role is not null).ToList();
        var memberIds = withRoles.Select(member => member.Id).ToList();
        var open = await db.RoleGrants
            .AsNoTracking()
            .Where(grant => memberIds.Contains(grant.MemberId) && grant.RevokedAt == null)
            .Select(grant => new { grant.MemberId, grant.Role })
            .ToListAsync(cancellationToken);

        var added = 0;
        foreach (var member in withRoles.Where(member => !open.Any(grant => grant.MemberId == member.Id && grant.Role == member.Role)))
        {
            db.RoleGrants.Add(RoleGrant.SystemGranted(member.RoleGrantId, member.Id, member.Role!.Value, clock.UtcNow));
            added++;
        }

        return added;
    }

    private async Task<int> AddDemoAccountsAsync(string demoPassword, CancellationToken cancellationToken)
    {
        var memberIds = DemoMembers.All.Select(member => member.Id).ToList();
        var existing = await db.Accounts
            .AsNoTracking()
            .Where(account => memberIds.Contains(account.MemberId))
            .Select(account => account.MemberId)
            .ToListAsync(cancellationToken);

        var added = 0;
        foreach (var member in DemoMembers.All.Where(member => !existing.Contains(member.Id)))
        {
            db.Accounts.Add(MemberAccount.Create(member.Id, member.Email, hasher.HashPassword(null!, demoPassword), clock.UtcNow));
            added++;
        }

        return added;
    }
}

public sealed record SeedResult(int SkillsAdded, int MembersAdded, int RolesAdded = 0, int AccountsAdded = 0);

public sealed class SeedConflictException(string message) : Exception(message);
