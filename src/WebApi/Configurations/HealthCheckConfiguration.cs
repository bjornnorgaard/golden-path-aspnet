using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using WebApi.HealthChecks;

namespace WebApi.Configurations;

/// <summary>
/// Liveness and readiness probes for an orchestrator to poll. Both are mapped with AllowAnonymous,
/// deliberately bypassing the global authentication fallback policy (see AuthenticationConfiguration)
/// - a platform's health checks have no session and must never be redirected into a login flow.
/// </summary>
public static class HealthCheckConfiguration
{
    private const string ReadyTag = "ready";

    extension(WebApplicationBuilder builder)
    {
        public void AddPlatformHealthChecks()
        {
            builder.Services
                .AddHealthChecks()
                .AddCheck<DatabaseHealthCheck>("database", tags: [ReadyTag]);
        }
    }

    extension(WebApplication app)
    {
        public void UsePlatformHealthChecks()
        {
            // Liveness: the process is up and able to handle requests. No dependency checks are run -
            // a struggling dependency should surface as a failed readiness check, not a restart.
            app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false })
                .AllowAnonymous();

            // Readiness: the app can actually serve traffic, e.g. the database is reachable.
            app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains(ReadyTag) })
                .AllowAnonymous();
        }
    }
}