using System.Net;
using System.Net.Http.Json;
using CircleShift.Api.Health;

namespace CircleShift.Tests.Api;

public sealed class HealthEndpointTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Live_returns_healthy_without_touching_dependencies()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<HealthStatusResponse>();
        Assert.NotNull(body);
        Assert.Equal("Healthy", body.Status);
        Assert.Equal("0.1.0", body.Version);
        Assert.Empty(body.Checks);
    }

    [Fact]
    public async Task Ready_returns_503_and_names_failed_checks_when_dependencies_are_down()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var raw = await response.Content.ReadAsStringAsync();
        var body = await response.Content.ReadFromJsonAsync<HealthStatusResponse>();
        Assert.NotNull(body);
        Assert.Equal("Unhealthy", body.Status);
        Assert.Equal("Unhealthy", body.Checks["database"]);
        Assert.Equal("Unhealthy", body.Checks["redis"]);

        // Connection details stay in the logs, never in the response.
        Assert.DoesNotContain("127.0.0.1", raw, StringComparison.Ordinal);
        Assert.DoesNotContain("Password", raw, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task OpenApi_document_lists_both_health_routes_in_development()
    {
        using var client = factory.CreateClient();

        var document = await client.GetStringAsync("/openapi/v1.json");

        Assert.Contains("\"/health/live\"", document, StringComparison.Ordinal);
        Assert.Contains("\"/health/ready\"", document, StringComparison.Ordinal);
    }
}
