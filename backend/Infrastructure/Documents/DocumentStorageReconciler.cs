using System.Security.Cryptography;
using System.Text;
using KeplerTalento.Application.Abstractions.Documents;
using KeplerTalento.Domain.Auditing;
using KeplerTalento.Domain.Documents;
using KeplerTalento.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace KeplerTalento.Infrastructure.Documents;

public sealed record DocumentReconciliationReport(
    int DocumentsChecked,
    int MissingObjects,
    int HashMismatches,
    int OrphanObjects,
    int StaleQuarantineObjects)
{
    public bool IsSuccessful => MissingObjects == 0 && HashMismatches == 0 && OrphanObjects == 0 && StaleQuarantineObjects == 0;
}

public sealed class DocumentStorageReconciler(
    ApplicationDbContext dbContext,
    IDocumentStorageInventory inventory)
{
    public async Task<DocumentReconciliationReport> ReconcileAsync(
        TimeSpan staleQuarantineAge,
        CancellationToken cancellationToken)
    {
        if (staleQuarantineAge <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(staleQuarantineAge));

        var documents = await dbContext.Documents.AsNoTracking().ToListAsync(cancellationToken);
        var available = await inventory.ListAvailableAsync(cancellationToken);
        var quarantine = await inventory.ListQuarantineAsync(cancellationToken);
        var availableByKey = available.ToDictionary(value => value.StorageKey, StringComparer.Ordinal);
        var quarantineByKey = quarantine.ToDictionary(value => value.StorageKey, StringComparer.Ordinal);
        var metadataKeys = documents.Select(value => value.StorageKey).ToHashSet(StringComparer.Ordinal);
        var now = DateTimeOffset.UtcNow;
        var correlationId = $"reconcile-{Guid.NewGuid():N}";
        var missing = 0;
        var mismatched = 0;
        var stale = 0;

        foreach (var document in documents)
        {
            var expectsAvailable = document.ScanState == DocumentScanState.Clean;
            var objects = expectsAvailable ? availableByKey : quarantineByKey;
            if (!objects.TryGetValue(document.StorageKey, out var storedObject))
            {
                missing++;
                AddAudit(document.Id.ToString("N"), "document.reconciliation.missing", correlationId, now);
                continue;
            }

            var actualHash = expectsAvailable
                ? await inventory.ComputeAvailableSha256Async(document.StorageKey, cancellationToken)
                : await inventory.ComputeQuarantineSha256Async(document.StorageKey, cancellationToken);
            if (!string.Equals(actualHash, document.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                mismatched++;
                AddAudit(document.Id.ToString("N"), "document.reconciliation.hash_mismatch", correlationId, now);
            }

            if (!expectsAvailable &&
                document.ScanState is DocumentScanState.PendingScan or DocumentScanState.ScanFailed &&
                storedObject.LastModifiedAtUtc <= now.Subtract(staleQuarantineAge))
            {
                stale++;
                AddAudit(document.Id.ToString("N"), "document.reconciliation.stale_quarantine", correlationId, now);
            }
        }

        var orphanObjects = available.Concat(quarantine)
            .Where(value => !metadataKeys.Contains(value.StorageKey))
            .ToList();
        foreach (var orphan in orphanObjects)
        {
            AddAudit(OpaqueSubject(orphan.StorageKey), "document.reconciliation.orphan", correlationId, now);
        }

        if (dbContext.ChangeTracker.HasChanges()) await dbContext.SaveChangesAsync(cancellationToken);
        return new(documents.Count, missing, mismatched, orphanObjects.Count, stale);
    }

    private void AddAudit(string subjectId, string outcomeCode, string correlationId, DateTimeOffset now) =>
        dbContext.AuditEvents.Add(new AuditEvent(
            Guid.NewGuid(),
            "document.reconciliation",
            subjectId,
            correlationId,
            now,
            outcomeCode));

    private static string OpaqueSubject(string storageKey)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(storageKey));
        return $"orphan-{Convert.ToHexString(hash.AsSpan(0, 8)).ToLowerInvariant()}";
    }
}
