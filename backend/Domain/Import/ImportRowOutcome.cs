namespace KeplerTalento.Domain.Import;

public static class ImportRowOutcomes
{
    public const string Loaded = "loaded";
    public const string Rejected = "rejected";
    public const string Skipped = "skipped";

    public static readonly IReadOnlyList<string> All = [Loaded, Rejected, Skipped];
}

/// <summary>
/// Which run an outcome belongs to. Validation is a dry run whose <c>loaded</c> means "would
/// load"; commit records what actually happened. They are separate facts, so they are separate
/// rows rather than one row updated in place.
/// </summary>
public static class ImportPhases
{
    public const string Validation = "validation";
    public const string Commit = "commit";

    public static readonly IReadOnlyList<string> All = [Validation, Commit];
}

/// <summary>
/// One data row's outcome in one phase of one batch.
/// </summary>
/// <remarks>
/// There is deliberately no field for the offending value, nor for any other value from the
/// row: a row is identified by its 1-based data row number and the name of the failing field,
/// and nothing else. This is the same control <c>RowProblem</c> carries, and removing it would
/// turn the report into a second copy of the import file.
///
/// An outcome is a fact about a run. It is inserted once and never updated — <c>ktl_runtime</c>
/// holds no <c>UPDATE</c> or <c>DELETE</c> on the table — and the unique key on
/// (batch, phase, row) is what makes a resumed commit unable to write a row twice.
/// </remarks>
public sealed class ImportRowOutcome
{
    private ImportRowOutcome() { }

    public ImportRowOutcome(
        Guid id,
        Guid batchId,
        string phase,
        int rowNumber,
        string outcome,
        string? field,
        string? reasonCode,
        Guid? candidateId,
        DateTimeOffset createdAtUtc)
    {
        if (!ImportPhases.All.Contains(phase, StringComparer.Ordinal))
        {
            throw new ArgumentOutOfRangeException(nameof(phase));
        }
        if (!ImportRowOutcomes.All.Contains(outcome, StringComparer.Ordinal))
        {
            throw new ArgumentOutOfRangeException(nameof(outcome));
        }
        if (rowNumber < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(rowNumber));
        }
        if (outcome != ImportRowOutcomes.Loaded && string.IsNullOrWhiteSpace(reasonCode))
        {
            throw new ArgumentException("A rejected or skipped row carries a reason code.", nameof(reasonCode));
        }
        if (reasonCode is not null && !ImportReasonCodes.IsKnown(reasonCode))
        {
            throw new ArgumentOutOfRangeException(nameof(reasonCode));
        }
        if (candidateId is not null && (outcome != ImportRowOutcomes.Loaded || phase != ImportPhases.Commit))
        {
            throw new ArgumentException("Only a committed, loaded row references a candidate.", nameof(candidateId));
        }
        Id = id;
        BatchId = batchId;
        Phase = phase;
        RowNumber = rowNumber;
        Outcome = outcome;
        Field = string.IsNullOrWhiteSpace(field) ? null : field.Trim();
        ReasonCode = reasonCode;
        CandidateId = candidateId;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid BatchId { get; private set; }
    public string Phase { get; private set; } = ImportPhases.Validation;
    public int RowNumber { get; private set; }
    public string Outcome { get; private set; } = ImportRowOutcomes.Loaded;

    /// <summary>The import file column that failed. A column name, never a value.</summary>
    public string? Field { get; private set; }
    public string? ReasonCode { get; private set; }
    public Guid? CandidateId { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
}
