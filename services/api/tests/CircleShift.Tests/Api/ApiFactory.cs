using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace CircleShift.Tests.Api;

/// <summary>
/// Starts the API in memory with PostgreSQL and Redis pointed at port 1, where
/// nothing listens, so readiness must report the dependencies as down.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("ConnectionStrings:Postgres", "Host=127.0.0.1;Port=1;Database=circleshift_test;Username=test;Password=test;Timeout=2");
        builder.UseSetting("ConnectionStrings:Redis", "127.0.0.1:1,abortConnect=false,connectTimeout=200");
    }
}
