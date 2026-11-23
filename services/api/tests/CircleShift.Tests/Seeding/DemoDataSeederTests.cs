using CircleShift.Application.Abstractions;
using CircleShift.Domain.Skills;
using CircleShift.Infrastructure.Persistence;
using CircleShift.Infrastructure.Seeding;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace CircleShift.Tests.Seeding;

public sealed class DemoDataSeederTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 20, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task First_run_adds_catalog_and_demo_members()
    {
        var options = NewDatabase();

        var result = await SeedAsync(options, includeDemoMembers: true);

        Assert.Equal(new SeedResult(SkillCatalog.Entries.Count, DemoMembers.All.Count), result);
        await using var db = new CircleShiftDbContext(options);
        Assert.Equal(SkillCatalog.Entries.Count, await db.Skills.CountAsync());
        Assert.Equal(DemoMembers.All.Count, await db.Members.CountAsync());
    }

    [Fact]
    public async Task Running_again_adds_nothing()
    {
        var options = NewDatabase();
        await SeedAsync(options, includeDemoMembers: true);

        var second = await SeedAsync(options, includeDemoMembers: true);

        Assert.Equal(new SeedResult(0, 0), second);
        await using var db = new CircleShiftDbContext(options);
        Assert.Equal(SkillCatalog.Entries.Count, await db.Skills.CountAsync());
    }

    [Fact]
    public async Task Catalog_only_run_skips_demo_members()
    {
        var options = NewDatabase();

        var result = await SeedAsync(options, includeDemoMembers: false);

        Assert.Equal(0, result.MembersAdded);
        await using var db = new CircleShiftDbContext(options);
        Assert.Empty(await db.Members.ToListAsync());
    }

    [Fact]
    public async Task Fills_in_missing_catalog_entries_only()
    {
        var options = NewDatabase();
        await using (var db = new CircleShiftDbContext(options))
        {
            db.Skills.Add(SkillCatalog.Get("gardening"));
            await db.SaveChangesAsync();
        }

        var result = await SeedAsync(options, includeDemoMembers: false);

        Assert.Equal(SkillCatalog.Entries.Count - 1, result.SkillsAdded);
    }

    [Fact]
    public async Task Slug_owned_by_another_id_stops_the_run()
    {
        var options = NewDatabase();
        await using (var db = new CircleShiftDbContext(options))
        {
            db.Skills.Add(Skill.Create(Guid.NewGuid(), "gardening", "Gardening", SkillCategory.Home));
            await db.SaveChangesAsync();
        }

        var error = await Assert.ThrowsAsync<SeedConflictException>(() => SeedAsync(options, includeDemoMembers: true));

        Assert.Contains("gardening", error.Message, StringComparison.Ordinal);
        await using var check = new CircleShiftDbContext(options);
        Assert.Equal(1, await check.Skills.CountAsync());
        Assert.Empty(await check.Members.ToListAsync());
    }

    private static DbContextOptions<CircleShiftDbContext> NewDatabase() =>
        new DbContextOptionsBuilder<CircleShiftDbContext>()
            .UseInMemoryDatabase($"seed-{Guid.NewGuid()}")
            .Options;

    private static async Task<SeedResult> SeedAsync(DbContextOptions<CircleShiftDbContext> options, bool includeDemoMembers)
    {
        await using var db = new CircleShiftDbContext(options);
        var seeder = new DemoDataSeeder(db, new FixedClock(Now), NullLogger<DemoDataSeeder>.Instance);
        return await seeder.SeedAsync(includeDemoMembers);
    }

    private sealed class FixedClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow => now;
    }
}
