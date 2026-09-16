namespace KeplerTalento.Application.Import.Rows;

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

    public IReadOnlyList<UnresolvedValue> UnresolvedValues { get; set; } = [];

    public IReadOnlyList<UnresolvedValue> BrokenMappings { get; set; } = [];

    /// <summary>Number of records the operator explicitly chose to overwrite.</summary>
    public int OverwrittenApplicationEdits { get; set; }

    public void Record(string entity, string sourceKey, RowOutcome outcome) =>
        _outcomes[(entity, sourceKey)] = outcome;

    public void AddProblem(RowProblem problem)
    {
        _problems.Add(problem);
        Record(problem.Entity, problem.SourceKey, RowOutcome.Rejected);
    }

    /// <summary>
    /// Records a reason without rejecting the row. Used where the row is sound and the
    /// loader is deliberately leaving it alone — an application-changed record or a
    /// duplicate is skipped, not rejected, and the distinction matters to whoever reads
    /// the report. The caller records the outcome itself.
    /// </summary>
    public void AddProblemWithoutRejecting(RowProblem problem) => _problems.Add(problem);

    public void AddUnmatched(UnmatchedTargetRecord record) => _unmatched.Add(record);

    public RowOutcome? OutcomeOf(string entity, string sourceKey) =>
        _outcomes.TryGetValue((entity, sourceKey), out var outcome) ? outcome : null;

    public int Count(RowOutcome outcome) => _outcomes.Count(entry => entry.Value == outcome);

    public int Count(string entity, RowOutcome outcome) =>
        _outcomes.Count(entry => entry.Key.Entity == entity && entry.Value == outcome);

    public int TotalRows => _outcomes.Count;

    public int UnmatchedCount => _unmatched.Count;

    public IReadOnlyList<string> SourceKeys(string entity, RowOutcome outcome) =>
        [.. _outcomes
            .Where(entry => entry.Key.Entity == entity && entry.Value == outcome)
            .Select(entry => entry.Key.SourceKey)
            .OrderBy(key => key, StringComparer.Ordinal)];
}
