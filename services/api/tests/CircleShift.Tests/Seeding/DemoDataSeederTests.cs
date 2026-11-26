using CircleShift.Application.Abstractions;
using CircleShift.Domain;
using CircleShift.Domain.Accounts;
using CircleShift.Domain.Members;
using CircleShift.Domain.Skills;
using CircleShift.Infrastructure.Persistence;
using CircleShift.Infrastructure.Seeding;
using Microsoft.AspNetCore.Identity;
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

        Assert.Equal(new SeedResult(SkillCatalog.Entries.Count, DemoMembers.All.Count, RolesAdded: 3, AccountsAdded: 0), result);
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

        Assert.Equal(new SeedResult(0, 0, 0, 0), second);
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

    [Fact]
    public async Task Persona_roles_are_seeded_as_verified_grants()
    {
        var options = NewDatabase();

        await SeedAsync(options, includeDemoMembers: true);

        await using var db = new CircleShiftDbContext(options);
        var grants = await db.RoleGrants.ToListAsync();
        Assert.All(grants, grant => Assert.True(grant.IsEffective));
        Assert.Equal(
            [
                (DemoMembers.All.Single(member => member.Handle == "marisol").Id, CommunityRole.Coordinator),
                (DemoMembers.All.Single(member => member.Handle == "andres").Id, CommunityRole.Moderator),
                (DemoMembers.All.Single(member => member.Handle == "camila").Id, CommunityRole.Admin),
            ],
            grants.OrderBy(grant => grant.Role).Select(grant => (grant.MemberId, grant.Role)).ToList());
    }

    [Fact]
    public async Task Demo_accounts_are_added_only_with_a_password_and_only_once()
    {
        var options = NewDatabase();
        await SeedAsync(options, includeDemoMembers: true);

        var withPassword = await SeedAsync(options, includeDemoMembers: true, "local-demo-password-1");
        var again = await SeedAsync(options, includeDemoMembers: true, "local-demo-password-1");

        Assert.Equal(DemoMembers.All.Count, withPassword.AccountsAdded);
        Assert.Equal(0, again.AccountsAdded);
        await using var db = new CircleShiftDbContext(options);
        var laura = await db.Accounts.SingleAsync(account => account.Email == "laura@demo.circleshift.example");
        Assert.NotEqual(
            PasswordVerificationResult.Failed,
            new PasswordHasher<MemberAccount>().VerifyHashedPassword(laura, laura.PasswordHash, "local-demo-password-1"));
    }

    [Fact]
    public async Task Short_demo_password_is_refused_before_anything_is_written()
    {
        var options = NewDatabase();

        var error = await Assert.ThrowsAsync<DomainException>(() => SeedAsync(options, includeDemoMembers: true, "short"));

        Assert.Equal("account.password_length", error.Code);
        await using var db = new CircleShiftDbContext(options);
        Assert.Empty(await db.Members.ToListAsync());
    }

    private static DbContextOptions<CircleShiftDbContext> NewDatabase() =>
        new DbContextOptionsBuilder<CircleShiftDbContext>()
            .UseInMemoryDatabase($"seed-{Guid.NewGuid()}")
            .Options;

    private static async Task<SeedResult> SeedAsync(DbContextOptions<CircleShiftDbContext> options, bool includeDemoMembers, string? demoPassword = null)
    {
        await using var db = new CircleShiftDbContext(options);
        var seeder = new DemoDataSeeder(db, new FixedClock(Now), new PasswordHasher<MemberAccount>(), NullLogger<DemoDataSeeder>.Instance);
        return await seeder.SeedAsync(includeDemoMembers, demoPassword);
    }

    private sealed class FixedClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow => now;
    }
}
