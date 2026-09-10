using KeplerTalento.Domain.Operations;
using KeplerTalento.Tools.DataMigration.Resolution;
using KeplerTalento.Tools.DataMigration.Validation;

namespace KeplerTalento.Tools.DataMigration.Loading;

/// <summary>
/// A record in the target carrying a source key the current export does not contain.
/// </summary>
/// <remarks>
/// Counted separately from rejections and skips on purpose. It usually means the row was
/// deleted in Access, but an export query missing a WHERE clause looks identical, so the
/// migration reports these and changes nothing about them. Deciding which it was is the
/// operator's job, and deactivating a live candidate on a malformed export is not a mistake
/// worth automating.
/// </remarks>
public sealed record UnmatchedTargetRecord(string Entity, string SourceKey);

/// <summary>
/// One source row's outcome. Every source row ends in exactly one of these.
/// </summary>
public enum RowOutcome
{
    Loaded,
    Rejected,
    Skipped,
}

public sealed class LoadResult
{
    private readonly Dictionary<(string Entity, string SourceKey), RowOutcome> _outcomes = new();
    private readonly List<RowProblem> _problems = [];
    private readonly List<UnmatchedTargetRecord> _unmatched = [];

    public IReadOnlyList<RowProblem> Problems => _problems;

    public IReadOnlyList<UnmatchedTargetRecord> UnmatchedTargetRecords =>
        [.. _unmatched.OrderBy(record => record.Entity, StringComparer.Ordinal)
            .ThenBy(record => record.SourceKey, StringComparer.Ordinal)];

    public IReadOnlyList<UnresolvedValue> UnresolvedValues { get; internal set; } = [];

    public IReadOnlyList<UnresolvedValue> BrokenMappings { get; internal set; } = [];

    /// <summary>Number of records the operator explicitly chose to overwrite.</summary>
    public int OverwrittenApplicationEdits { get; internal set; }

    internal void Record(string entity, string sourceKey, RowOutcome outcome) =>
        _outcomes[(entity, sourceKey)] = outcome;

    internal void AddProblem(RowProblem problem)
    {
        _problems.Add(problem);
        Record(problem.Entity, problem.SourceKey, RowOutcome.Rejected);
    }

    /// <summary>
    /// Records a reason without rejecting the row. Used where the row is sound and the
    /// migration is deliberately leaving it alone — an application-changed record is
    /// skipped, not rejected, and the distinction matters to the operator reading the
    /// report.
    /// </summary>
    internal void AddProblemWithoutRejecting(RowProblem problem) => _problems.Add(problem);

    internal void AddUnmatched(UnmatchedTargetRecord record) => _unmatched.Add(record);

    public int Count(RowOutcome outcome) => _outcomes.Count(entry => entry.Value == outcome);

    public int Count(string entity, RowOutcome outcome) =>
        _outcomes.Count(entry => entry.Key.Entity == entity && entry.Value == outcome);

    public int TotalRows => _outcomes.Count;

    public IReadOnlyList<string> SourceKeys(string entity, RowOutcome outcome) =>
        [.. _outcomes
            .Where(entry => entry.Key.Entity == entity && entry.Value == outcome)
            .Select(entry => entry.Key.SourceKey)
            .OrderBy(key => key, StringComparer.Ordinal)];

    public MigrationRunCounts ToCounts(int sourceRows) => new(
        SourceRows: sourceRows,
        Loaded: Count(RowOutcome.Loaded),
        Rejected: Count(RowOutcome.Rejected),
        Skipped: Count(RowOutcome.Skipped),
        UnmatchedTargetRecords: _unmatched.Count,
        UnresolvedValues: UnresolvedValues.Count,
        DocumentsLoaded: Count(MigrationEntities.Document, RowOutcome.Loaded),
        DocumentsRejected: Count(MigrationEntities.Document, RowOutcome.Rejected));
}
