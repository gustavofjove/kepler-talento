using KeplerTalento.Application.Abstractions.Documents;
using KeplerTalento.Application.Abstractions.Import;
using KeplerTalento.Application.Import;
using KeplerTalento.Application.Import.Rows;
using KeplerTalento.Domain.Import;
using KeplerTalento.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace KeplerTalento.Infrastructure.Import;

/// <summary>
/// What validation and commit share: opening the admitted file, sniffing it, reading its rows
/// under the contract limits, and building the evaluator. Keeping it in one place is what keeps
/// the dry run and the real run from drifting apart.
/// </summary>
public sealed class ImportRunSupport(
    ApplicationDbContext dbContext,
    IDocumentStorage storage,
    IImportFileInspector inspector,
    IImportRowReader reader,
    ImportOptions options)
{
    /// <summary>
    /// Opens the promoted file. Only the promoted copy is ever read: a file still in quarantine is
    /// not reachable from here at all.
    /// </summary>
    public async Task<Stream?> OpenAdmittedAsync(ImportBatch batch, CancellationToken cancellationToken)
    {
        if (!await storage.AvailableExistsAsync(batch.StorageKey, cancellationToken))
        {
            return null;
        }
        return await storage.OpenAvailableAsync(batch.StorageKey, cancellationToken);
    }

    /// <summary>
    /// Sniffs the file, then reads it once without evaluating anything: this is where a structural
    /// problem or a row count over the limit is found <em>before</em> any row is judged. Returns the
    /// normalized email addresses in the file, which the duplicate rule needs up front.
    /// </summary>
    public async Task<(int RowCount, HashSet<string> Emails)> PrepareAsync(ImportBatch batch, CancellationToken cancellationToken)
    {
        await using (var sniffed = await OpenAdmittedAsync(batch, cancellationToken)
            ?? throw new ImportStructuralException(ImportReasonCodes.FileMissing))
        {
            var inspection = await inspector.InspectAsync(sniffed, cancellationToken);
            if (!inspection.Accepted)
            {
                throw new ImportStructuralException(inspection.Code);
            }
        }

        var emails = new HashSet<string>(StringComparer.Ordinal);
        var rowCount = 0;
        await foreach (var row in ReadRowsAsync(batch, cancellationToken))
        {
            rowCount++;
            var email = CandidateImportRowEvaluator.NormalizeEmail(row[CandidateImportContract.Email]);
            if (email.Length > 0)
            {
                emails.Add(email);
            }
        }
        return (rowCount, emails);
    }

    public async IAsyncEnumerable<ImportFileRow> ReadRowsAsync(
        ImportBatch batch,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await using var content = await OpenAdmittedAsync(batch, cancellationToken)
            ?? throw new ImportStructuralException(ImportReasonCodes.FileMissing);
        await foreach (var row in reader.ReadAsync(
            content,
            CandidateImportContract.RequiredColumns,
            CandidateImportContract.KnownColumns,
            new ImportLimits(options.MaximumRows, options.MaximumBytes),
            cancellationToken))
        {
            yield return row;
        }
    }

    /// <summary>
    /// The shared resolver over every catalog entry, active or retired — the same population the
    /// operator migration resolves against, so the two apply identical matching rules. It never
    /// creates an entry.
    /// </summary>
    public async Task<CatalogResolver> CreateResolverAsync(CancellationToken cancellationToken)
    {
        var entries = await dbContext.CatalogItems
            .AsNoTracking()
            .Select(item => new CatalogResolver.CatalogEntry(item.Id, item.Family, item.Code, item.NameEs))
            .ToListAsync(cancellationToken);
        return new CatalogResolver(entries);
    }

    /// <summary>
    /// Which of the file's addresses a candidate already holds, active or not — excluding the
    /// candidates this batch itself created, so a resumed commit judges its remaining rows against
    /// the database as the uninterrupted run saw it.
    /// </summary>
    public async Task<HashSet<string>> ExistingEmailsAsync(
        IReadOnlyCollection<string> emails,
        IReadOnlyCollection<Guid> createdByThisBatch,
        CancellationToken cancellationToken)
    {
        var existing = new HashSet<string>(StringComparer.Ordinal);
        foreach (var chunk in emails.Chunk(500))
        {
            var found = await dbContext.Candidates
                .AsNoTracking()
                .Where(candidate => chunk.Contains(candidate.Email.ToLower()) && !createdByThisBatch.Contains(candidate.Id))
                .Select(candidate => candidate.Email.ToLower())
                .ToListAsync(cancellationToken);
            existing.UnionWith(found);
        }
        return existing;
    }
}
