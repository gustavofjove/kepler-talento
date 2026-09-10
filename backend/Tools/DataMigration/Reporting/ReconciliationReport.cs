using System.Text.Json.Serialization;

namespace KeplerTalento.Tools.DataMigration.Reporting;

/// <summary>
/// The reconciliation report: what the run did, in terms an operator can act on and an
/// auditor can keep.
/// </summary>
/// <remarks>
/// Nothing in this shape can carry candidate personal data. Rows are cited by source key,
/// entity and field name; the only source <em>values</em> that appear are unresolved catalog
/// values, deliberately, because an operator cannot write a mapping file without them — and
/// those are business vocabulary (languages, sectors, skills), not anyone's details.
/// </remarks>
public sealed record ReconciliationReport
{
    public required Guid RunId { get; init; }
    public required string Verb { get; init; }
    public required DateTimeOffset StartedAtUtc { get; init; }
    public required DateTimeOffset FinishedAtUtc { get; init; }
    public required string Outcome { get; init; }

    /// <summary>The backup that undoes this run. Null for verbs that write nothing.</summary>
    public string? PreMigrationBackup { get; init; }

    public required bool Reconciles { get; init; }

    /// <summary>Set when the operator asked for application-changed records to be overwritten.</summary>
    public int OverwrittenApplicationEdits { get; init; }

    public required IReadOnlyList<EntityTally> Entities { get; init; }
    public required IReadOnlyList<RejectedRow> Rejected { get; init; }
    public required IReadOnlyList<SkippedRow> Skipped { get; init; }
    public required IReadOnlyList<UnresolvedEntry> Unresolved { get; init; }
    public required IReadOnlyList<UnresolvedEntry> BrokenMappings { get; init; }
    public required IReadOnlyList<UnmatchedEntry> UnmatchedTargetRecords { get; init; }
    public required IReadOnlyList<DocumentVerification> Documents { get; init; }

    /// <summary>
    /// The steps the operator still owes after reading this, repeated here because the
    /// report is the artifact that outlives the terminal session.
    /// </summary>
    public required IReadOnlyList<string> Checklist { get; init; }

    public sealed record EntityTally(string Entity, int SourceRows, int Loaded, int Rejected, int Skipped)
    {
        [JsonIgnore]
        public bool Reconciles => SourceRows == Loaded + Rejected + Skipped;
    }

    public sealed record RejectedRow(string Entity, string SourceKey, string Field, string ReasonCode);

    public sealed record SkippedRow(string Entity, string SourceKey, string ReasonCode);

    public sealed record UnresolvedEntry(string Family, string Value, int Occurrences);

    public sealed record UnmatchedEntry(string Entity, string SourceKey);

    public sealed record DocumentVerification(string SourceKey, string Result, string? ReasonCode);
}
