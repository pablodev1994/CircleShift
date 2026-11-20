using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CircleShift.Infrastructure.Persistence;

/// <summary>
/// Used by <c>dotnet ef</c>. Reads CIRCLESHIFT_POSTGRES; the fallback has no
/// password and is only good enough for commands that never connect, such as
/// adding a migration or checking for pending model changes.
/// </summary>
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<CircleShiftDbContext>
{
    public CircleShiftDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("CIRCLESHIFT_POSTGRES")
            ?? "Host=127.0.0.1;Port=5432;Database=circleshift;Username=circleshift";

        var options = new DbContextOptionsBuilder<CircleShiftDbContext>()
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history"))
            .Options;

        return new CircleShiftDbContext(options);
    }
}
