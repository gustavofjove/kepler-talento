using KeplerTalento.Application.Abstractions.Operations;
using KeplerTalento.Application.Features.Import;
using KeplerTalento.Domain.Import;
using KeplerTalento.Domain.Operations;
using KeplerTalento.Infrastructure.Operations;
using KeplerTalento.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace KeplerTalento.Infrastructure.Import;

/// <summary>
/// Start-up recovery for import batches, and the purge schedule.
/// </summary>
/// <remarks>
/// <para>
/// A worker that dies mid-operation leaves its claim to expire, and the durable operation worker
/// picks it up again — that covers most restarts. What it does not cover is an operation that ran
/// out of attempts, or a crash between recording a transition and queuing its operation. Either
/// would leave a batch in a transient state with nothing going to move it, so on start-up every
/// batch still <c>uploaded</c>, <c>scanning</c>, <c>validating</c> or <c>committing</c> without a live
/// operation gets a new one. A commit found this way <em>resumes</em>: the commit handler skips the
/// rows it already wrote, so recovery never restarts a batch from row one.
/// </para>
/// </remarks>
public sealed class ImportMaintenanceService(
    IServiceScopeFactory scopeFactory,
    OperationWorkerOptions workerOptions,
    ImportOptions importOptions,
    ILogger<ImportMaintenanceService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!workerOptions.Enabled)
        {
            return;
        }

        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(importOptions.PurgeIntervalMinutes));
        do
        {
            // On start-up and then on every tick: an operation that exhausts its attempts while the
            // process keeps running would otherwise wait for the next restart.
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var recovered = await RecoverAsync(scope.ServiceProvider, stoppingToken);
                if (recovered > 0)
                {
                    logger.LogInformation("Import recovery re-queued {RecoveredBatches} batches", recovered);
                }
            }
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogWarning(exception, "Import recovery failed with code {FailureCode}", "import.recovery.failed");
            }
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var operations = scope.ServiceProvider.GetRequiredService<IOperationRepository>();
                var slot = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / (importOptions.PurgeIntervalMinutes * 60L);
                await operations.EnqueueAsync(ImportOperations.Purge, $"import-purge-{slot}", $"{ImportOperations.Purge}:{slot}", stoppingToken);
            }
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogWarning(exception, "Import purge scheduling failed with code {FailureCode}", "import.purge.schedule_failed");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    public static async Task<int> RecoverAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        var dbContext = services.GetRequiredService<ApplicationDbContext>();
        var operations = services.GetRequiredService<IOperationRepository>();
        var stuck = await dbContext.ImportBatches
            .Where(batch => ImportBatchStates.Transient.Contains(batch.State))
            .ToListAsync(cancellationToken);
        var recovered = 0;
        foreach (var batch in stuck)
        {
            var type = batch.State switch
            {
                ImportBatchStates.Uploaded or ImportBatchStates.Scanning => ImportOperations.Scan,
                ImportBatchStates.Validating => ImportOperations.Validate,
                _ => ImportOperations.Commit,
            };
            var key = ImportOperations.Key(type, batch.Id, batch.OperationAttempt);
            var current = await dbContext.Operations.AsNoTracking()
                .SingleOrDefaultAsync(operation => operation.IdempotencyKey == key, cancellationToken);
            if (current is { Status: OperationStatus.Queued or OperationStatus.Running })
            {
                // Live, or claimed by a worker whose lease will expire and be reclaimed.
                continue;
            }
            if (current is not null)
            {
                batch.IncrementOperationAttempt(DateTimeOffset.UtcNow);
                await dbContext.SaveChangesAsync(cancellationToken);
                key = ImportOperations.Key(type, batch.Id, batch.OperationAttempt);
            }
            await operations.EnqueueAsync(type, $"import-recovery-{batch.Id:N}", key, cancellationToken);
            recovered++;
        }
        return recovered;
    }
}
