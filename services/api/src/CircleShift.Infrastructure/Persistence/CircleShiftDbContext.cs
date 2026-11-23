using CircleShift.Domain.Members;
using CircleShift.Domain.Skills;
using Microsoft.EntityFrameworkCore;

namespace CircleShift.Infrastructure.Persistence;

public sealed class CircleShiftDbContext(DbContextOptions<CircleShiftDbContext> options) : DbContext(options)
{
    public DbSet<Member> Members => Set<Member>();

    public DbSet<Skill> Skills => Set<Skill>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CircleShiftDbContext).Assembly);
}
