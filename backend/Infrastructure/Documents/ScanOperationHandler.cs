using KeplerTalento.Application.Abstractions.Documents;
using KeplerTalento.Domain.Auditing;
using KeplerTalento.Domain.Documents;
using KeplerTalento.Domain.Operations;
using KeplerTalento.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace KeplerTalento.Infrastructure.Documents;

public sealed record ScanOperationOutcome(bool Completed, string Code);

public sealed class ScanOperationHandler(
    ApplicationDbContext dbContext,
    IDocumentStorage storage,
    IMalwareScanner scanner)
{
    public async Task<ScanOperationOutcome> HandleAsync(Operation operation, CancellationToken cancellationToken)
    {
        if (!TryGetDocumentId(operation.IdempotencyKey, out var documentId)) return new(false, "scan.document.invalid");
        var document = await dbContext.Documents.SingleOrDefaultAsync(value => value.Id == documentId, cancellationToken);
        if (document is null) return new(false, "scan.document.missing");
        if (document.ScanState == DocumentScanState.Clean && await storage.AvailableExistsAsync(document.StorageKey, cancellationToken))
        {
            return new(true, "scanner.clean");
        }
        await using var content = await storage.OpenQuarantineAsync(document.StorageKey, cancellationToken);
        var result = await scanner.ScanAsync(content, cancellationToken);
        var now = DateTimeOffset.UtcNow;
        switch (result.Verdict)
        {
            case ScanVerdict.Clean:
                await storage.PromoteAsync(document.StorageKey, cancellationToken);
                document.MarkClean(result.Signature, now);
                break;
            case ScanVerdict.Infected:
                document.MarkUnavailable(DocumentScanState.Infected, result.Code, now);
                break;
            default:
                document.MarkUnavailable(DocumentScanState.ScanFailed, result.Code, now);
                break;
        }
        dbContext.AuditEvents.Add(new AuditEvent(Guid.NewGuid(), "document.scan", document.Id.ToString("N"), operation.CorrelationId, now));
        await dbContext.SaveChangesAsync(cancellationToken);
        return result.Verdict == ScanVerdict.Error ? new(false, result.Code) : new(true, result.Code);
    }

    private static bool TryGetDocumentId(string idempotencyKey, out Guid documentId)
    {
        documentId = Guid.Empty;
        var segments = idempotencyKey.Split(':', StringSplitOptions.RemoveEmptyEntries);
        return segments.Length == 3 && segments[0] == "document" && segments[2] == "scan" && Guid.TryParse(segments[1], out documentId);
    }
}
