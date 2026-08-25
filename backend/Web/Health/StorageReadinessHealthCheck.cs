using KeplerTalento.Infrastructure.Documents;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace KeplerTalento.Web.Health;

public sealed class StorageReadinessHealthCheck(DocumentStorageOptions options) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            FileSystemDocumentStorage.ValidateAndPrepare(options);
            return Task.FromResult(HealthCheckResult.Healthy("storage_ready"));
        }
        catch (Exception exception)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy("storage_unavailable", exception));
        }
    }
}
