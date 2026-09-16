namespace KeplerTalento.Application.Abstractions.Import;

/// <summary>The contract limits the reader enforces while it streams (design D9).</summary>
public sealed record ImportLimits(int MaximumRows, long MaximumBytes);

/// <summary>One data row, by 1-based data row number, keyed by normalized column name.</summary>
/// <remarks>
/// Holds imported personal data. It lives for the duration of one row's evaluation, is never
/// logged, and has no <c>ToString</c> that would reveal its values if something tried.
/// </remarks>
public sealed class ImportFileRow(int rowNumber, IReadOnlyDictionary<string, string> values, bool shapeValid)
{
    public int RowNumber { get; } = rowNumber;

    /// <summary>False when the record had a different number of fields than the header.</summary>
    public bool ShapeValid { get; } = shapeValid;

    public string this[string column] => values.TryGetValue(column, out var value) ? value : string.Empty;

    public override string ToString() => $"row {RowNumber}";
}

/// <summary>
/// A problem with the file's shape rather than its rows. Carries a stable code and, at most, a
/// column name or a limit — never a value from a data row.
/// </summary>
public sealed class ImportStructuralException(string code, string? detail = null) : Exception(code)
{
    public string Code { get; } = code;
    public string? Detail { get; } = detail;
}

/// <summary>
/// Reads an import file as a stream of rows, stopping at the documented limits rather than
/// buffering the file and checking afterwards. One implementation per accepted content type;
/// CSV is the only one today (design D8).
/// </summary>
public interface IImportRowReader
{
    string ContentType { get; }

    /// <summary>
    /// Validates the header against <paramref name="requiredColumns"/> and
    /// <paramref name="knownColumns"/> before yielding any row, then yields data rows in order.
    /// Throws <see cref="ImportStructuralException"/> for a header problem, a row count over the
    /// limit, a file over the byte limit, invalid encoding or unparseable structure.
    /// </summary>
    IAsyncEnumerable<ImportFileRow> ReadAsync(
        Stream content,
        IReadOnlyCollection<string> requiredColumns,
        IReadOnlyCollection<string> knownColumns,
        ImportLimits limits,
        CancellationToken cancellationToken);
}

public sealed record ImportFileInspection(bool Accepted, string Code);

/// <summary>
/// Content sniffing for an import file. Runs after the scan and before parsing (design D6): a
/// ".csv" that is really a spreadsheet container is exactly what an attacker sends.
/// </summary>
public interface IImportFileInspector
{
    Task<ImportFileInspection> InspectAsync(Stream content, CancellationToken cancellationToken);
}

/// <summary>
/// The import file's private storage. Built on the KTL-9 document storage mechanism under its
/// own key prefix, so an import file is never addressable as a candidate document.
/// </summary>
public interface IImportFileStorage
{
    string CreateKey(Guid batchId);

    long MaximumBytes { get; }

    /// <summary>
    /// Accepts a file name only when its extension is on the import allowlist. The content is
    /// checked separately, after the scan.
    /// </summary>
    bool IsAllowedFileName(string fileName);
}
