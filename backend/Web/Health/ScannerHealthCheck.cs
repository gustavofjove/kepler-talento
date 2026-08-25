using System.Net.Sockets;
using KeplerTalento.Infrastructure.Documents;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace KeplerTalento.Web.Health;

public sealed class ScannerHealthCheck(ClamAvOptions options) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(Math.Min(options.TimeoutSeconds, 5)));
            using var client = new TcpClient();
            await client.ConnectAsync(options.Host, options.Port, timeout.Token);
            return HealthCheckResult.Healthy("scanner_ready");
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Degraded("scanner_unavailable", exception);
        }
    }
}
