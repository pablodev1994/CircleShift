using CircleShift.Api.Health;
using CircleShift.Infrastructure;
using CircleShift.Infrastructure.Persistence;
using CircleShift.Infrastructure.Seeding;

var builder = WebApplication.CreateBuilder(args);

var postgres = builder.Configuration.GetConnectionString("Postgres")
    ?? throw new InvalidOperationException("ConnectionStrings:Postgres is not configured.");
var redis = builder.Configuration.GetConnectionString("Redis")
    ?? throw new InvalidOperationException("ConnectionStrings:Redis is not configured.");

builder.Services.AddCircleShiftInfrastructure(postgres);
builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();

// Readiness covers PostgreSQL (source of truth) and Redis (cache and, later,
// the SignalR backplane). Liveness runs no checks at all.
builder.Services.AddHealthChecks()
    .AddDbContextCheck<CircleShiftDbContext>(name: "database", tags: [HealthTags.Ready])
    .AddRedis(redis, name: "redis", tags: [HealthTags.Ready], timeout: TimeSpan.FromSeconds(3));

var app = builder.Build();

// `dotnet run -- seed [--demo-members]` loads the canonical skill catalog
// (and, in Development only, the fictional demo members), then exits.
if (args is ["seed", .. var seedOptions])
{
    var includeDemoMembers = seedOptions.Contains("--demo-members");
    if (includeDemoMembers && !app.Environment.IsDevelopment())
    {
        Console.Error.WriteLine("Demo members are only seeded in the Development environment.");
        return 1;
    }

    await using var scope = app.Services.CreateAsyncScope();
    var result = await scope.ServiceProvider.GetRequiredService<DemoDataSeeder>().SeedAsync(includeDemoMembers);
    Console.WriteLine($"Skills added: {result.SkillsAdded}; demo members added: {result.MembersAdded}");
    return 0;
}

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapControllers();

await app.RunAsync();
return 0;

public partial class Program;
