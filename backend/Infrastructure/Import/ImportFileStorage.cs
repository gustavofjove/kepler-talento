using KeplerTalento.Application.Abstractions.Import;
using KeplerTalento.Application.Import;

namespace KeplerTalento.Infrastructure.Import;

/// <summary>
/// Import-file specifics over the shared private storage: an opaque key under its own prefix, the
/// import size limit and the import allowlist. The bytes go through <c>IDocumentStorage</c> —
/// quarantine first, promoted only on a clean verdict — rather than a second implementation.
/// </summary>
public sealed class ImportFileStorage(ImportOptions options) : IImportFileStorage
{
    /// <summary>
    /// Every import file lives under this prefix, which is disjoint from the candidate document
    /// prefix, so an import file can never be addressed as a candidate document and document
    /// reconciliation can tell the two apart.
    /// </summary>
    public const string KeyPrefix = "imports/";

    public long MaximumBytes => options.MaximumBytes;

    public string CreateKey(Guid batchId) => $"{KeyPrefix}{batchId:N}/content";

    public bool IsAllowedFileName(string fileName) =>
        string.Equals(Path.GetExtension(fileName ?? string.Empty), CandidateImportContract.AcceptedExtension, StringComparison.OrdinalIgnoreCase);

    public static bool TryGetBatchId(string storageKey, out Guid batchId)
    {
        batchId = Guid.Empty;
        if (!storageKey.StartsWith(KeyPrefix, StringComparison.Ordinal))
        {
            return false;
        }
        var segments = storageKey[KeyPrefix.Length..].Split('/');
        return segments.Length == 2 && segments[1] == "content" && Guid.TryParseExact(segments[0], "N", out batchId);
    }
}
