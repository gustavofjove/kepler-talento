namespace KeplerTalento.Domain.Operations;

/// <summary>
/// The verbs the migration tool can be invoked with.
/// </summary>
public static class MigrationVerbs
{
    public const string Validate = "validate";
    public const string Load = "load";
    public const string Report = "report";

    public static readonly IReadOnlyList<string> All = [Validate, Load, Report];

    public static bool IsKnown(string? verb) =>
        verb is not null && All.Contains(verb, StringComparer.Ordinal);
}

public static class MigrationOutcomes
{
    public const string Running = "running";
    public const string Reconciled = "reconciled";
    public const string NotReconciled = "not_reconciled";
    public const string Failed = "failed";

    public static readonly IReadOnlyList<string> All = [Running, Reconciled, NotReconciled, Failed];

    public static bool IsKnown(string? outcome) =>
        outcome is not null && All.Contains(outcome, StringComparer.Ordinal);
}

/// <summary>
/// Tallies for one migration run. Every source row lands in exactly one of
/// <see cref="Loaded"/>, <see cref="Rejected"/> or <see cref="Skipped"/>;
/// <see cref="UnmatchedTargetRecords"/> counts in the opposite direction and is deliberately
/// kept separate so an incomplete export cannot be read as a set of deletions.
/// </summary>
public readonly record struct MigrationRunCounts(
    int SourceRows,
    int Loaded,
    int Rejected,
    int Skipped,
    int UnmatchedTargetRecords,
    int UnresolvedValues,
    int DocumentsLoaded,
    int DocumentsRejected)
{
    public bool Reconciles => SourceRows == Loaded + Rejected + Skipped;
}

/// <summary>
/// The durable record of one invocation of the migration tool. It carries no personal data:
/// counts, an outcome, the label of the backup taken before the run, and the reconciliation
/// report, which cites rows by source identifier and field name only.
/// </summary>
public sealed class MigrationRun
{
    private MigrationRun() { }

    public MigrationRun(Guid id, string verb, string? backupLabel, DateTimeOffset startedAtUtc)
    {
        if (!MigrationVerbs.IsKnown(verb))
        {
            throw new ArgumentOutOfRangeException(nameof(verb));
        }
        Id = id;
        Verb = verb;
        BackupLabel = string.IsNullOrWhiteSpace(backupLabel) ? null : backupLabel.Trim();
        StartedAtUtc = startedAtUtc;
        Outcome = MigrationOutcomes.Running;
    }

    public Guid Id { get; private set; }
    public string Verb { get; private set; } = string.Empty;
    public string Outcome { get; private set; } = MigrationOutcomes.Running;

    /// <summary>
    /// The label of the backup taken before this run. The report repeats it in its header,
    /// so the artifact that proves the migration also names the thing that undoes it.
    /// </summary>
    public string? BackupLabel { get; private set; }

    public DateTimeOffset StartedAtUtc { get; private set; }
    public DateTimeOffset? FinishedAtUtc { get; private set; }
    public int SourceRows { get; private set; }
    public int LoadedRows { get; private set; }
    public int RejectedRows { get; private set; }
    public int SkippedRows { get; private set; }
    public int UnmatchedTargetRecords { get; private set; }
    public int UnresolvedValues { get; private set; }
    public int DocumentsLoaded { get; private set; }
    public int DocumentsRejected { get; private set; }

    /// <summary>
    /// The reconciliation report as emitted, so <c>report</c> can re-emit a recorded run
    /// without the export set still being present.
    /// </summary>
    public string? ReportJson { get; private set; }

    public void Finish(
        string outcome,
        MigrationRunCounts counts,
        string? reportJson,
        DateTimeOffset finishedAtUtc)
    {
        if (!MigrationOutcomes.IsKnown(outcome) || outcome == MigrationOutcomes.Running)
        {
            throw new ArgumentOutOfRangeException(nameof(outcome));
        }
        Outcome = outcome;
        SourceRows = counts.SourceRows;
        LoadedRows = counts.Loaded;
        RejectedRows = counts.Rejected;
        SkippedRows = counts.Skipped;
        UnmatchedTargetRecords = counts.UnmatchedTargetRecords;
        UnresolvedValues = counts.UnresolvedValues;
        DocumentsLoaded = counts.DocumentsLoaded;
        DocumentsRejected = counts.DocumentsRejected;
        ReportJson = reportJson;
        FinishedAtUtc = finishedAtUtc;
    }
}
