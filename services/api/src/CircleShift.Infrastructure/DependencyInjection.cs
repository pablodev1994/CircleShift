using CircleShift.Application.Abstractions;
using CircleShift.Infrastructure.Persistence;
using CircleShift.Infrastructure.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CircleShift.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddCircleShiftInfrastructure(this IServiceCollection services, string postgresConnectionString)
    {
        services.AddDbContext<CircleShiftDbContext>(options =>
            options.UseNpgsql(postgresConnectionString, npgsql =>
                npgsql.MigrationsHistoryTable("__ef_migrations_history")));

        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IClock, SystemClock>();
        return services;
    }
}
