using CircleShift.Infrastructure.Accounts;
using CircleShift.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CircleShift.Tests.Api;

/// <summary>
/// The API in memory with an EF Core in-memory database (one per factory), so
/// account, session and permission flows run without PostgreSQL.
/// </summary>
public class AccountsApiFactory : WebApplicationFactory<Program>
{
    private readonly string databaseName = $"accounts-{Guid.NewGuid()}";

    protected virtual int AuthPermitPerMinute => 1000;

    protected virtual bool SecureCookies => true;

    public ApiSession NewSession() =>
        new(CreateClient(new WebApplicationFactoryClientOptions
        {
            // Secure cookies are only sent back over "https".
            BaseAddress = new Uri(SecureCookies ? "https://localhost" : "http://localhost"),
            AllowAutoRedirect = false,
        }));

    public async Task<T> WithServiceAsync<T, TService>(Func<TService, Task<T>> action)
        where TService : notnull
    {
        await using var scope = Services.CreateAsyncScope();
        return await action(scope.ServiceProvider.GetRequiredService<TService>());
    }

    public Task<Guid> MakeFirstAdminAsync(Guid memberId) =>
        WithServiceAsync<Guid, RoleGrantService>(async grants => (await grants.BootstrapFirstAdminAsync(memberId)).Id);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("ConnectionStrings:Postgres", "Host=127.0.0.1;Port=1;Database=unused;Username=test;Password=test");
        builder.UseSetting("ConnectionStrings:Redis", "127.0.0.1:1,abortConnect=false,connectTimeout=200");
        // Production cookie settings (Secure, __Host- prefix) even though the
        // environment is Development; DevelopmentCookieTests covers plain http.
        builder.UseSetting("Auth:SecureCookies", SecureCookies ? "true" : "false");
        builder.UseSetting("RateLimits:AuthPermitPerMinute", AuthPermitPerMinute.ToString(System.Globalization.CultureInfo.InvariantCulture));
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<CircleShiftDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<CircleShiftDbContext>>();
            services.AddDbContext<CircleShiftDbContext>(options => options.UseInMemoryDatabase(databaseName));
        });
    }
}

public sealed class StrictRateLimitApiFactory : AccountsApiFactory
{
    protected override int AuthPermitPerMinute => 2;
}

public sealed class PlainHttpDevelopmentApiFactory : AccountsApiFactory
{
    protected override bool SecureCookies => false;
}
