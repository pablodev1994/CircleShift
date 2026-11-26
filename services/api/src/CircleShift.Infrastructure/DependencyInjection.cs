using CircleShift.Application.Abstractions;
using CircleShift.Domain.Accounts;
using CircleShift.Infrastructure.Accounts;
using CircleShift.Infrastructure.Persistence;
using CircleShift.Infrastructure.Profiles;
using CircleShift.Infrastructure.Seeding;
using CircleShift.Infrastructure.Time;
using Microsoft.AspNetCore.Identity;
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
        services.AddSingleton<IPasswordHasher<MemberAccount>, PasswordHasher<MemberAccount>>();
        services.AddScoped<AccountService>();
        services.AddScoped<MemberAccessService>();
        services.AddScoped<RoleGrantService>();
        services.AddScoped<ProfileService>();
        services.AddScoped<DemoDataSeeder>();
        return services;
    }
}
