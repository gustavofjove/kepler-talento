using KeplerTalento.Application.Import.Rows;
using KeplerTalento.Domain.Operations;
using KeplerTalento.Tools.DataMigration.Validation;

namespace KeplerTalento.Tools.DataMigration.Loading;

/// <summary>
/// The migration-run counts for a load. Kept in the tool because document counts are a
/// migration concept; the shared <see cref="LoadResult"/> knows nothing about entities.
/// </summary>
public static class LoadResultCounts
{
    public static MigrationRunCounts ToCounts(this LoadResult result, int sourceRows) => new(
        SourceRows: sourceRows,
        Loaded: result.Count(RowOutcome.Loaded),
        Rejected: result.Count(RowOutcome.Rejected),
        Skipped: result.Count(RowOutcome.Skipped),
        UnmatchedTargetRecords: result.UnmatchedCount,
        UnresolvedValues: result.UnresolvedValues.Count,
        DocumentsLoaded: result.Count(MigrationEntities.Document, RowOutcome.Loaded),
        DocumentsRejected: result.Count(MigrationEntities.Document, RowOutcome.Rejected));
}
