namespace KeplerTalento.Domain.Import;

/// <summary>
/// The closed catalogue of reasons a data row is not loaded, and of the structural and file
/// refusals of a batch. Stable strings: the page translates them, an operator matches on them,
/// and <c>docs/ktl-17/row-reason-codes.md</c> explains each. A row outcome can only carry a code
/// from <see cref="RowCodes"/>; the check constraint on the table holds it to that set.
/// </summary>
public static class ImportReasonCodes
{
    // Row-level: the row was understood and fails a rule.
    public const string FieldRequired = "field.required";
    public const string FieldTooLong = "field.too_long";
    public const string EmailInvalid = "email.invalid";
    public const string DateInvalid = "date.invalid";
    public const string StatusUnknown = "status.unknown";
    public const string ReferenceMalformed = "reference.malformed";
    public const string ReferenceUnresolved = "reference.unresolved";
    public const string ReferenceDuplicate = "reference.duplicate";
    public const string RowShapeInvalid = "row.shape_invalid";

    /// <summary>
    /// The candidate rules a direct write enforces refused the row. Reached only when an import
    /// check above did not already catch it — it exists so no weaker candidate is ever loaded.
    /// </summary>
    public const string CandidateRefused = "candidate.refused";

    /// <summary>
    /// Skipped, not rejected (design D5): the row describes a person already present, in this
    /// file or in the database. A re-run of a committed file must not read as a wall of failures.
    /// </summary>
    public const string CandidateDuplicate = "candidate.duplicate";

    // Batch-level: the file, not a row.
    public const string ColumnMissing = "import.column.missing";
    public const string ColumnUnknown = "import.column.unknown";
    public const string ColumnDuplicate = "import.column.duplicate";
    public const string HeaderMissing = "import.header.missing";
    public const string RowLimitExceeded = "import.rows.limit_exceeded";
    public const string FileTooLarge = "import.file.too_large";
    public const string FileEmpty = "import.file.empty";
    public const string FileEncodingInvalid = "import.file.encoding_invalid";
    public const string FileMalformed = "import.file.malformed";
    public const string FileTypeNotAllowed = "import.file.type_not_allowed";
    public const string FileContentMismatch = "import.file.content_mismatch";
    public const string FileMissing = "import.file.missing";
    public const string FileChanged = "import.file.changed";
    public const string ScanInfected = "import.scan.infected";
    public const string ScanUnscannable = "import.scan.unscannable";
    public const string RunInterrupted = "import.run.interrupted";

    public static readonly IReadOnlyList<string> RowCodes =
    [
        FieldRequired,
        FieldTooLong,
        EmailInvalid,
        DateInvalid,
        StatusUnknown,
        ReferenceMalformed,
        ReferenceUnresolved,
        ReferenceDuplicate,
        RowShapeInvalid,
        CandidateRefused,
        CandidateDuplicate,
    ];

    public static readonly IReadOnlyList<string> BatchCodes =
    [
        ColumnMissing,
        ColumnUnknown,
        ColumnDuplicate,
        HeaderMissing,
        RowLimitExceeded,
        FileTooLarge,
        FileEmpty,
        FileEncodingInvalid,
        FileMalformed,
        FileTypeNotAllowed,
        FileContentMismatch,
        FileMissing,
        FileChanged,
        ScanInfected,
        ScanUnscannable,
        RunInterrupted,
    ];

    public static bool IsKnown(string? code) => code is not null && RowCodes.Contains(code, StringComparer.Ordinal);
}
