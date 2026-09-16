using KeplerTalento.Application.Abstractions.Documents;
using KeplerTalento.Application.Features.Import;
using KeplerTalento.Domain.Import;
using KeplerTalento.Domain.Operations;
using KeplerTalento.Infrastructure.Documents;
using KeplerTalento.Infrastructure.Operations;
using KeplerTalento.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace KeplerTalento.Infrastructure.Import;

public sealed record ImportPurgeReport(int FilesPurged, int MissingFilesRecorded, int OrphansRemoved);

/// <summary>
/// Removes uploaded import files once their batch has been closed longer than the retention
/// window (design D7), and leaves no orphan in either direction.
/// </summary>
/// <remarks>
/// <para>
/// What survives a purge is the batch row — state, counts, failure code, unresolved catalog
/// values — and every row outcome. None of it is personal data. What is lost is the file, and with
/// it the ability to say exactly what a row contained; the runbook says so plainly.
/// </para>
/// <para>
/// "No orphan" is enforced both ways. A stored import file with no batch (an upload interrupted
/// between writing the file and recording the batch) is removed once it is older than the grace
/// period. A closed batch whose file has disappeared is marked purged, so it reports its file as
/// no longer retained instead of failing obscurely the next time something asks for it.
/// </para>
/// </remarks>
public sealed class ImportPurgeHandler(
    ApplicationDbContext dbContext,
    IDocumentStorage storage,
    IDocumentStorageInventory inventory,
    ImportOptions options,
    ILogger<ImportPurgeHandler> logger) : IOperationHandler
{
    public string Type => ImportOperations.Purge;

    public async Task<ScanOperationOutcome> HandleAsync(Operation operation, CancellationToken cancellationToken)
    {
        var report = await PurgeAsync(DateTimeOffset.UtcNow, cancellationToken);
        return new(true, $"import.purge.completed:{report.FilesPurged}:{report.MissingFilesRecorded}:{report.OrphansRemoved}");
    }

    public async Task<ImportPurgeReport> PurgeAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var cutoff = now.AddDays(-options.RetentionDays);
        var due = await dbContext.ImportBatches
            .Where(batch => ImportBatchStates.Closed.Contains(batch.State)
                && batch.FilePurgedAtUtc == null
                && batch.ClosedAtUtc != null
                && batch.ClosedAtUtc <= cutoff)
            .ToListAsync(cancellationToken);
        foreach (var batch in due)
        {
            await storage.DeleteQuarantineIfExistsAsync(batch.StorageKey, cancellationToken);
            await storage.DeleteAvailableIfExistsAsync(batch.StorageKey, cancellationToken);
            batch.MarkFilePurged(now);
        }
        await dbContext.SaveChangesAsync(cancellationToken);

        // A closed batch whose file is already gone says so, rather than pointing at nothing.
        var available = (await inventory.ListAvailableAsync(cancellationToken))
            .Where(value => value.StorageKey.StartsWith(ImportFileStorage.KeyPrefix, StringComparison.Ordinal))
            .ToList();
        var quarantined = (await inventory.ListQuarantineAsync(cancellationToken))
            .Where(value => value.StorageKey.StartsWith(ImportFileStorage.KeyPrefix, StringComparison.Ordinal))
            .ToList();
        var storedKeys = available.Concat(quarantined).Select(value => value.StorageKey).ToHashSet(StringComparer.Ordinal);
        var missing = await dbContext.ImportBatches
            .Where(batch => ImportBatchStates.Closed.Contains(batch.State) && batch.FilePurgedAtUtc == null)
            .ToListAsync(cancellationToken);
        var missingRecorded = 0;
        foreach (var batch in missing.Where(batch => !storedKeys.Contains(batch.StorageKey)))
        {
            batch.MarkFilePurged(now);
            missingRecorded++;
        }
        await dbContext.SaveChangesAsync(cancellationToken);

        // Stored import files with no batch, or whose batch is already purged.
        var liveKeys = (await dbContext.ImportBatches
                .AsNoTracking()
                .Where(batch => batch.FilePurgedAtUtc == null)
                .Select(batch => batch.StorageKey)
                .ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.Ordinal);
        var graceCutoff = now.AddHours(-options.OrphanGraceHours);
        var orphans = 0;
        foreach (var stored in available.Where(value => !liveKeys.Contains(value.StorageKey) && value.LastModifiedAtUtc <= graceCutoff))
        {
            await storage.DeleteAvailableIfExistsAsync(stored.StorageKey, cancellationToken);
            orphans++;
        }
        foreach (var stored in quarantined.Where(value => !liveKeys.Contains(value.StorageKey) && value.LastModifiedAtUtc <= graceCutoff))
        {
            await storage.DeleteQuarantineIfExistsAsync(stored.StorageKey, cancellationToken);
            orphans++;
        }

        var report = new ImportPurgeReport(due.Count, missingRecorded, orphans);
        logger.LogInformation(
            "Import purge removed {FilesPurged} files, recorded {MissingFilesRecorded} missing files and removed {OrphansRemoved} orphans",
            report.FilesPurged,
            report.MissingFilesRecorded,
            report.OrphansRemoved);
        return report;
    }
}
