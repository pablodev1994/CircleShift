using CircleShift.Api.Authorization;
using CircleShift.Api.Errors;
using CircleShift.Api.Health;
using CircleShift.Domain;
using CircleShift.Domain.Accounts;
using CircleShift.Infrastructure;
using CircleShift.Infrastructure.Accounts;
using CircleShift.Infrastructure.Persistence;
using CircleShift.Infrastructure.Seeding;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var postgres = builder.Configuration.GetConnectionString("Postgres")
    ?? throw new InvalidOperationException("ConnectionStrings:Postgres is not configured.");
var redis = builder.Configuration.GetConnectionString("Redis")
    ?? throw new InvalidOperationException("ConnectionStrings:Redis is not configured.");

builder.Services.AddCircleShiftInfrastructure(postgres);
builder.Services.AddCircleShiftAuth(builder.Configuration);
builder.Services
    .AddControllers(options =>
    {
        // Cookie sessions need CSRF protection on every unsafe method.
        options.Filters.Add<CsrfValidationFilter>();
    })
    .ConfigureApiBehaviorOptions(options =>
        options.InvalidModelStateResponseFactory = context =>
        {
            var details = context.ModelState
                .Where(entry => entry.Value is { Errors.Count: > 0 })
                .ToDictionary(
                    entry => entry.Key,
                    entry => entry.Value!.Errors.Select(error => string.IsNullOrEmpty(error.ErrorMessage) ? "Invalid value." : error.ErrorMessage).ToArray());
            return new BadRequestObjectResult(
                ApiErrors.For(context.HttpContext, "request.invalid", "The request body is not valid.", details));
        });
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<DomainExceptionHandler>();

// Readiness covers PostgreSQL (source of truth) and Redis (cache and, later,
// the SignalR backplane). Liveness runs no checks at all.
builder.Services.AddHealthChecks()
    .AddDbContextCheck<CircleShiftDbContext>(name: "database", tags: [HealthTags.Ready])
    .AddRedis(redis, name: "redis", tags: [HealthTags.Ready], timeout: TimeSpan.FromSeconds(3));

var app = builder.Build();

// `dotnet run -- seed [--demo-members]` loads the canonical skill catalog
// (and, in Development only, the fictional demo members with their persona
// roles; sign-in accounts too when CIRCLESHIFT_DEMO_PASSWORD is set), then exits.
if (args is ["seed", .. var seedOptions])
{
    var includeDemoMembers = seedOptions.Contains("--demo-members");
    if (includeDemoMembers && !app.Environment.IsDevelopment())
    {
        Console.Error.WriteLine("Demo members are only seeded in the Development environment.");
        return 1;
    }

    var demoPassword = Environment.GetEnvironmentVariable("CIRCLESHIFT_DEMO_PASSWORD");
    await using var scope = app.Services.CreateAsyncScope();
    try
    {
        var result = await scope.ServiceProvider.GetRequiredService<DemoDataSeeder>()
            .SeedAsync(includeDemoMembers, string.IsNullOrEmpty(demoPassword) ? null : demoPassword);
        Console.WriteLine(
            $"Skills added: {result.SkillsAdded}; demo members added: {result.MembersAdded}; " +
            $"demo roles added: {result.RolesAdded}; demo accounts added: {result.AccountsAdded}");
        if (includeDemoMembers && string.IsNullOrEmpty(demoPassword))
        {
            Console.WriteLine("CIRCLESHIFT_DEMO_PASSWORD is not set, so demo members have no sign-in accounts.");
        }
    }
    catch (DomainException error)
    {
        Console.Error.WriteLine($"CIRCLESHIFT_DEMO_PASSWORD: {error.Message}");
        return 1;
    }

    return 0;
}

// `dotnet run -- bootstrap-admin <email>` makes a registered member the first
// admin. It refuses once a verified admin exists; after that, admins grant roles.
if (args is ["bootstrap-admin", var adminEmail])
{
    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<CircleShiftDbContext>();
    try
    {
        var email = MemberAccount.NormalizeEmail(adminEmail);
        var account = await db.Accounts.AsNoTracking().SingleOrDefaultAsync(candidate => candidate.Email == email);
        if (account is null)
        {
            Console.Error.WriteLine("No account with that email. Register first.");
            return 1;
        }

        var grant = await scope.ServiceProvider.GetRequiredService<RoleGrantService>().BootstrapFirstAdminAsync(account.MemberId);
        Console.WriteLine($"Member {grant.MemberId} is now an admin (grant {grant.Id}).");
        return 0;
    }
    catch (DomainException error)
    {
        Console.Error.WriteLine(error.Message);
        return 1;
    }
}

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
}

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapControllers();

await app.RunAsync();
return 0;

public partial class Program;
