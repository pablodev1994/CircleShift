using CircleShift.Api.Health;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CircleShift.Api.Controllers;

[ApiController]
[Route("health")]
[Produces("application/json")]
public sealed class HealthController(HealthCheckService healthChecks) : ControllerBase
{
    private static readonly string Version =
        typeof(HealthController).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";

    /// <summary>The process is up and serving requests. No dependencies are checked.</summary>
    [HttpGet("live")]
    [ProducesResponseType<HealthStatusResponse>(StatusCodes.Status200OK)]
    public ActionResult<HealthStatusResponse> Live() =>
        Ok(new HealthStatusResponse(HealthStatus.Healthy.ToString(), Version, new Dictionary<string, string>()));

    /// <summary>PostgreSQL and Redis are reachable. Returns 503 when any check fails.</summary>
    [HttpGet("ready")]
    [ProducesResponseType<HealthStatusResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<HealthStatusResponse>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<HealthStatusResponse>> Ready(CancellationToken cancellationToken)
    {
        var report = await healthChecks.CheckHealthAsync(
            registration => registration.Tags.Contains(HealthTags.Ready),
            cancellationToken);

        var body = new HealthStatusResponse(
            report.Status.ToString(),
            Version,
            report.Entries.ToDictionary(entry => entry.Key, entry => entry.Value.Status.ToString()));

        return report.Status == HealthStatus.Unhealthy
            ? StatusCode(StatusCodes.Status503ServiceUnavailable, body)
            : Ok(body);
    }
}
