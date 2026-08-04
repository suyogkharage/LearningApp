using LearningApp.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace LearningApp.Api.HealthChecks;

public sealed class SqlDatabaseHealthCheck(IServiceScopeFactory scopeFactory) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<LearningAppDbContext>();

            var canConnect = await dbContext.Database.CanConnectAsync(cancellationToken);

            return canConnect
                ? HealthCheckResult.Healthy("SQL database connection is healthy.")
                : HealthCheckResult.Unhealthy("SQL database connection failed.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("SQL database health check failed.", ex);
        }
    }
}
