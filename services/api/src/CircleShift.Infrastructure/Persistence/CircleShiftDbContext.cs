using CircleShift.Domain.Accounts;
using CircleShift.Domain.Members;
using CircleShift.Domain.Skills;
using Microsoft.EntityFrameworkCore;

namespace CircleShift.Infrastructure.Persistence;

public sealed class CircleShiftDbContext(DbContextOptions<CircleShiftDbContext> options) : DbContext(options)
{
    public DbSet<Member> Members => Set<Member>();

    public DbSet<Skill> Skills => Set<Skill>();

    public DbSet<MemberAccount> Accounts => Set<MemberAccount>();

    public DbSet<RoleGrant> RoleGrants => Set<RoleGrant>();

    public DbSet<MemberBlock> MemberBlocks => Set<MemberBlock>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CircleShiftDbContext).Assembly);
}
