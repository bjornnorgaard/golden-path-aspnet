using Microsoft.Extensions.Diagnostics.HealthChecks;
using WebApi.Database;

namespace WebApi.HealthChecks;

public sealed class DatabaseHealthCheck(TodoContext dbContext) : IHealthCheck
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
