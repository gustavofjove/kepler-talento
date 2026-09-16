using KeplerTalento.Domain.Import;

namespace KeplerTalento.Application.Abstractions.Persistence;

public enum ImportBatchSaveOutcome
{
    Saved,

    /// <summary>
    /// The batch changed after the caller read the version it declared. For a commit claim this
    /// is the answer that makes two concurrent commits impossible: only one
    /// <c>validated → committing</c> write can match the version both of them read.
    /// </summary>
    ConcurrencyConflict,
}

public sealed record ImportBatchPage(IReadOnlyList<ImportBatch> Items, int TotalCount);

public sealed record ImportRowOutcomePage(IReadOnlyList<ImportRowOutcome> Items, int TotalCount);

/// <summary>Persistence for import batches and their row outcomes.</summary>
/// <remarks>
/// There is no <c>Remove</c>, for batches or for outcomes. A batch is a record of work that
/// happened; its file is purged on schedule but the row survives. An outcome is a fact about a
/// run and is never updated either — <c>ktl_runtime</c> holds only <c>SELECT, INSERT</c> on it.
/// </remarks>
public interface IImportBatchRepository
{
    Task<ImportBatch?> FindAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Most recent first, paged.</summary>
    Task<ImportBatchPage> ListAsync(int page, int pageSize, CancellationToken cancellationToken);

    /// <summary>
    /// For each content hash, when a batch with that content was first committed — so the page
    /// can warn about a deliberate re-import (design D4). Never a refusal.
    /// </summary>
    Task<IReadOnlyDictionary<string, DateTimeOffset>> EarliestCommitByHashAsync(
        IEnumerable<string> sha256Values,
        CancellationToken cancellationToken);

    /// <summary>
    /// The row report for one phase, ordered by row number. Outcomes carry no value; paging
    /// exists because a 2000-row file is a long page, not because the rows are sensitive.
    /// </summary>
    Task<ImportRowOutcomePage> ListOutcomesAsync(
        Guid batchId,
        string phase,
        int page,
        int pageSize,
        CancellationToken cancellationToken);

    void Add(ImportBatch batch);

    /// <summary>
    /// Declares the version the caller read, so a transition against a stale version is refused
    /// rather than silently racing a concurrent one.
    /// </summary>
    void ExpectVersion(ImportBatch batch, uint version);

    Task<ImportBatchSaveOutcome> SaveAsync(CancellationToken cancellationToken);
}
