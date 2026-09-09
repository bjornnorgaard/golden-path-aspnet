using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using WebApi.Database;

namespace WebApi.Configurations;

public static class HealthChecksConfiguration
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
        public void MapPlatformHealthChecks()
        {
            // Liveness: the process is up and able to handle requests. Runs no registered checks -
            // in particular, no DB round-trip - so a dependency outage can't fail this probe.
            app.MapHealthChecks("/health", new HealthCheckOptions
            {
                Predicate = _ => false
            }).ExcludeFromDescription();

            // Readiness: the app can actually serve traffic, including reaching the database.
            app.MapHealthChecks("/ready", new HealthCheckOptions
            {
                Predicate = check => check.Tags.Contains(ReadyTag)
            }).ExcludeFromDescription();
        }
    }

    private sealed class DatabaseHealthCheck(TodoContext dbContext) : IHealthCheck
    {
        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
        {
            var canConnect = await dbContext.Database.CanConnectAsync(ct);
            if (canConnect)
            {
                return HealthCheckResult.Healthy();
            }

            return HealthCheckResult.Unhealthy("Unable to connect to the database.");
        }
    }
}