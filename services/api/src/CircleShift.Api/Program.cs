using CircleShift.Api.Health;
using CircleShift.Infrastructure;
using CircleShift.Infrastructure.Persistence;

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

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapControllers();

app.Run();

public partial class Program;
