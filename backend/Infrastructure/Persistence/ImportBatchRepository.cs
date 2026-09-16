using KeplerTalento.Application.Abstractions.Persistence;
using KeplerTalento.Domain.Import;
using Microsoft.EntityFrameworkCore;

namespace KeplerTalento.Infrastructure.Persistence;

/// <summary>Import batch persistence.</summary>
public sealed class ImportBatchRepository(ApplicationDbContext dbContext) : IImportBatchRepository
{
    public Task<ImportBatch?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.ImportBatches.SingleOrDefaultAsync(batch => batch.Id == id, cancellationToken);

    public async Task<ImportBatchPage> ListAsync(int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = dbContext.ImportBatches.AsNoTracking();
        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(batch => batch.CreatedAtUtc)
            .ThenByDescending(batch => batch.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
        return new ImportBatchPage(items, totalCount);
    }

    public async Task<IReadOnlyDictionary<string, DateTimeOffset>> EarliestCommitByHashAsync(
        IEnumerable<string> sha256Values,
        CancellationToken cancellationToken)
    {
        var hashes = sha256Values.Distinct(StringComparer.Ordinal).ToList();
        if (hashes.Count == 0)
        {
            return new Dictionary<string, DateTimeOffset>();
        }
        var commits = await dbContext.ImportBatches
            .AsNoTracking()
            .Where(batch => hashes.Contains(batch.Sha256) && batch.CommittedAtUtc != null)
            .GroupBy(batch => batch.Sha256)
            .Select(group => new { Sha256 = group.Key, CommittedAt = group.Min(batch => batch.CommittedAtUtc)!.Value })
            .ToListAsync(cancellationToken);
        return commits.ToDictionary(commit => commit.Sha256, commit => commit.CommittedAt, StringComparer.Ordinal);
    }

    public async Task<ImportRowOutcomePage> ListOutcomesAsync(
        Guid batchId,
        string phase,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query = dbContext.ImportRowOutcomes
            .AsNoTracking()
            .Where(outcome => outcome.BatchId == batchId && outcome.Phase == phase);
        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(outcome => outcome.RowNumber)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
        return new ImportRowOutcomePage(items, totalCount);
    }

    public void Add(ImportBatch batch) => dbContext.ImportBatches.Add(batch);

    public void ExpectVersion(ImportBatch batch, uint version) =>
        dbContext.Entry(batch).Property(entity => entity.Version).OriginalValue = version;

    public async Task<ImportBatchSaveOutcome> SaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return ImportBatchSaveOutcome.ConcurrencyConflict;
        }
        // xmin is a system column the write does not bring back; reload so the response carries
        // the version the next transition must send.
        foreach (var entry in dbContext.ChangeTracker.Entries<ImportBatch>().ToList())
        {
            await entry.ReloadAsync(cancellationToken);
        }
        return ImportBatchSaveOutcome.Saved;
    }
}
