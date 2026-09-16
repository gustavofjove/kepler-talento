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

/// <summary>
/// Scans an uploaded import file and admits it only on a clean verdict.
/// </summary>
/// <remarks>
/// <para>
/// The file stays in quarantine until the scanner says <c>Clean</c>, and is then promoted. The
/// validation and commit handlers read the <em>promoted</em> file only, so "nothing parses the
/// file before it is scanned" is a property of where they look rather than a check each of them
/// has to remember.
/// </para>
/// <para>
/// An infected file and a file the scanner cannot judge are both terminal: the batch leaves the
/// scan path and no state transition leads back into it, so no retry admits the file.
/// </para>
/// </remarks>
public sealed class ImportScanHandler(
    ApplicationDbContext dbContext,
    IDocumentStorage storage,
    IMalwareScanner scanner,
    ILogger<ImportScanHandler> logger) : IOperationHandler
{
    public string Type => ImportOperations.Scan;

    public async Task<ScanOperationOutcome> HandleAsync(Operation operation, CancellationToken cancellationToken)
    {
        if (!ImportOperations.TryParseKey(Type, operation.IdempotencyKey, out var batchId))
        {
            return new(false, "import.operation.invalid");
        }
        var batch = await dbContext.ImportBatches.SingleOrDefaultAsync(value => value.Id == batchId, cancellationToken);
        if (batch is null)
        {
            return new(false, "import.batch.missing");
        }
        var now = DateTimeOffset.UtcNow;
        if (batch.State == ImportBatchStates.Uploaded)
        {
            batch.StartScan(now);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        if (batch.State != ImportBatchStates.Scanning)
        {
            // Scanned or past it already; a retried operation has nothing left to do.
            return new(true, "import.scan.already_decided");
        }

        // A previous attempt may have promoted the file and died before recording it.
        if (await storage.AvailableExistsAsync(batch.StorageKey, cancellationToken))
        {
            batch.MarkScanned(now);
            await dbContext.SaveChangesAsync(cancellationToken);
            return new(true, "scanner.clean");
        }

        ScanResult result;
        try
        {
            await using var content = await storage.OpenQuarantineAsync(batch.StorageKey, cancellationToken);
            result = await scanner.ScanAsync(content, cancellationToken);
        }
        catch (FileNotFoundException)
        {
            result = new ScanResult(ScanVerdict.Error, ImportReasonCodes.FileMissing);
        }
        catch (DirectoryNotFoundException)
        {
            result = new ScanResult(ScanVerdict.Error, ImportReasonCodes.FileMissing);
        }

        switch (result.Verdict)
        {
            case ScanVerdict.Clean:
                await storage.PromoteAsync(batch.StorageKey, cancellationToken);
                batch.MarkScanned(now);
                break;
            case ScanVerdict.Infected:
                batch.MarkInfected(ImportReasonCodes.ScanInfected, now);
                break;
            default:
                batch.MarkUnscannable(ImportReasonCodes.ScanUnscannable, now);
                break;
        }
        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation(
            "Import batch {BatchId} scan finished in state {BatchState} with code {OutcomeCode}",
            batch.Id,
            batch.State,
            result.Code);
        return new(true, result.Code);
    }
}
