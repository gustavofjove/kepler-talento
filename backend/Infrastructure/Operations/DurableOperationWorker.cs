using KeplerTalento.Application.Abstractions.Operations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using KeplerTalento.Infrastructure.Documents;

namespace KeplerTalento.Infrastructure.Operations;

public sealed class DurableOperationWorker(
    IServiceScopeFactory scopeFactory,
    OperationWorkerOptions options,
    ILogger<DurableOperationWorker> logger) : BackgroundService
{
    private readonly string _owner = $"worker-{Guid.NewGuid():N}";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Enabled) return;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var repository = scope.ServiceProvider.GetRequiredService<IOperationRepository>();
                var operation = await repository.ClaimNextAsync(_owner, TimeSpan.FromSeconds(options.LeaseSeconds), stoppingToken);
                if (operation is not null)
                {
                    if (operation.Type == "document.scan")
                    {
                        var handler = scope.ServiceProvider.GetRequiredService<ScanOperationHandler>();
                        var handling = handler.HandleAsync(operation, stoppingToken);
                        while (!handling.IsCompleted)
                        {
                            var delay = Task.Delay(TimeSpan.FromSeconds(Math.Max(1, options.LeaseSeconds / 2)), stoppingToken);
                            if (await Task.WhenAny(handling, delay) == delay)
                            {
                                if (!await repository.RenewLeaseAsync(operation.Id, _owner, TimeSpan.FromSeconds(options.LeaseSeconds), stoppingToken))
                                {
                                    throw new InvalidOperationException("operation.lease.lost");
                                }
                            }
                        }
                        var outcome = await handling;
                        if (outcome.Completed)
                        {
                            await repository.CompleteAsync(operation.Id, _owner, outcome.Code, stoppingToken);
                        }
                        else
                        {
                            await repository.FailAsync(operation.Id, _owner, outcome.Code, stoppingToken);
                        }
                    }
                    else
                    {
                        await repository.FailAsync(operation.Id, _owner, "operation.handler.not_registered", stoppingToken);
                    }
                }
            }
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogWarning(exception, "Durable operation polling failed with code {FailureCode}", "operation.poll.failed");
            }
            await Task.Delay(TimeSpan.FromSeconds(options.PollSeconds), stoppingToken);
        }
    }
}
