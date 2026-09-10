namespace KeplerTalento.Tools.DataMigration.Validation;

/// <summary>
/// One reason a source row cannot be loaded, or was left alone.
/// </summary>
/// <remarks>
/// There is deliberately no field for the offending <em>value</em>. A rejected row is
/// identified by its source key and the name of the failing field, and nothing else — which
/// is what keeps candidate personal data out of the reconciliation report and the logs. The
/// absence of that parameter is the control; a reviewer adding one would be removing it.
/// </remarks>
public sealed record RowProblem(string Entity, string SourceKey, string Field, string ReasonCode)
{
    public override string ToString() => $"{Entity}/{SourceKey}: {Field} {ReasonCode}";
}

/// <summary>
/// A problem with the shape of the export rather than with the data in it. These may carry
/// detail, because they describe files and columns — never a candidate's values.
/// </summary>
public sealed record StructuralProblem(string File, string Detail)
{
    public override string ToString() => $"{File}: {Detail}";
}

public static class MigrationEntities
{
    public const string Candidate = "candidate";
    public const string Language = "language";
    public const string Program = "program";
    public const string Education = "education";
    public const string Experience = "experience";
    public const string Skill = "skill";
    public const string Document = "document";
}

/// <summary>
/// The closed set of reasons a row is not loaded. Stable strings: an operator matches on
/// them, and the runbook explains what to do about each.
/// </summary>
public static class ReasonCodes
{
    public const string SourceKeyMissing = "source_key.missing";
    public const string SourceKeyDuplicate = "source_key.duplicate";
    public const string RequiredFieldMissing = "field.required";
    public const string EmailInvalid = "email.invalid";
    public const string DateInvalid = "date.invalid";
    public const string BooleanInvalid = "boolean.invalid";
    public const string IntegerInvalid = "integer.invalid";
    public const string StatusUnknown = "status.unknown";
    public const string ConsentMissing = "consent.missing";
    public const string DeletedAtInconsistent = "deleted_at.inconsistent";
    public const string PeriodInconsistent = "period.inconsistent";
    public const string UnknownCandidate = "candidate.unknown";
    public const string CandidateRejected = "candidate.rejected";
    public const string ReferenceUnresolved = "reference.unresolved";

    /// <summary>
    /// The database refused part of the aggregate. Recorded by constraint name at most —
    /// never the failing value, and never the driver's message, which can quote parameters.
    /// </summary>
    public const string LoadFailed = "load.failed";

    public const string DocumentMissingFile = "document.file.missing";
    public const string DocumentPathUnsafe = "document.path.unsafe";
    public const string DocumentHashMismatch = "document.hash.mismatch";
    public const string DocumentRejectedByScanner = "document.scan.rejected";
    public const string DocumentUnscannable = "document.scan.unscannable";
    public const string DocumentFormatNotAllowed = "document.format.not_allowed";
    public const string DocumentTooLarge = "document.size.exceeded";
    public const string DocumentNotStored = "document.not_stored";

    /// <summary>Skipped rather than rejected: the application owns this row now.</summary>
    public const string ApplicationChanged = "target.application_changed";
}
