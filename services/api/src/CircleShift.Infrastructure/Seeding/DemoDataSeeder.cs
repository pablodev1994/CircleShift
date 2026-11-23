using CircleShift.Application.Abstractions;
using CircleShift.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CircleShift.Infrastructure.Seeding;

/// <summary>
/// Inserts the canonical skill catalog and, when asked, the fictional demo
/// members. Safe to run repeatedly: rows that already exist are left alone,
/// and a catalog slug owned by a different id stops the run instead of
/// creating a second skill with the same meaning.
/// </summary>
public sealed class DemoDataSeeder(CircleShiftDbContext db, IClock clock, ILogger<DemoDataSeeder> logger)
{
    public async Task<SeedResult> SeedAsync(bool includeDemoMembers, CancellationToken cancellationToken = default)
    {
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
        }

        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Seed finished: {SkillsAdded} skills added ({SkillTotal} in catalog), {MembersAdded} demo members added",
            skillsAdded,
            SkillCatalog.Entries.Count,
            membersAdded);

        return new SeedResult(skillsAdded, membersAdded);
    }
}

public sealed record SeedResult(int SkillsAdded, int MembersAdded);

public sealed class SeedConflictException(string message) : Exception(message);
