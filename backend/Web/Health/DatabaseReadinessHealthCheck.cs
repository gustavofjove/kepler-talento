using KeplerTalento.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace KeplerTalento.Web.Health;

public sealed class DatabaseReadinessHealthCheck(IServiceScopeFactory scopeFactory) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            if (!await dbContext.Database.CanConnectAsync(cancellationToken))
            {
                return HealthCheckResult.Unhealthy("database_unavailable");
            }
            var pending = await dbContext.Database.GetPendingMigrationsAsync(cancellationToken);
            return pending.Any()
                ? HealthCheckResult.Unhealthy("database_migrations_pending")
                : HealthCheckResult.Healthy("database_ready");
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy("database_unavailable", exception);
        }
    }
}
