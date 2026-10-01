using System.Linq.Expressions;

namespace KeplerTalento.Domain.Documents;

public enum DocumentScanState
{
    PendingScan,
    Clean,
    Infected,
    Rejected,
    ScanFailed,
}

public sealed class CandidateDocument
{
    /// <summary>The only content type the client renders inline.</summary>
    public const string PreviewableContentType = "application/pdf";

    /// <summary>
    /// Whether the document can be rendered inside the application (KTL-35): a clean scan, a
    /// natively renderable format, and a binary that actually exists (a legacy record without
    /// one is not). An expression so list queries evaluate it in SQL; it must stay in step with
    /// the «Available» availability of <c>CandidateDocumentResponse</c>.
    /// </summary>
    public static readonly Expression<Func<CandidateDocument, bool>> IsPreviewable = document =>
        document.ScanState == DocumentScanState.Clean
        && document.ContentType == PreviewableContentType
        && !(document.SourceKey != null && (document.Sha256 == null || document.Sha256.Trim() == string.Empty));

    /// <summary>
    /// Whether the document can be downloaded at all (KTL-35): a clean scan and a binary that
    /// actually exists, in any allowed format. It must stay in step with the «Available»
    /// availability of <c>CandidateDocumentResponse</c>.
    /// </summary>
    public static readonly Expression<Func<CandidateDocument, bool>> IsDownloadable = document =>
        document.ScanState == DocumentScanState.Clean
        && !(document.SourceKey != null && (document.Sha256 == null || document.Sha256.Trim() == string.Empty));

    private static readonly Func<CandidateDocument, bool> IsPreviewableCompiled = IsPreviewable.Compile();
    private static readonly Func<CandidateDocument, bool> IsDownloadableCompiled = IsDownloadable.Compile();

    /// <summary>In-memory form of <see cref="IsDownloadable"/>.</summary>
    public bool CanBeDownloaded => IsDownloadableCompiled(this);

    /// <summary>In-memory form of <see cref="IsPreviewable"/>.</summary>
    public bool CanBePreviewed => IsPreviewableCompiled(this);

    private CandidateDocument() { }

    public CandidateDocument(
        Guid id,
        Guid candidateId,
        string storageKey,
        string originalFileName,
        string contentType,
        long size,
        string sha256,
        DateTimeOffset createdAtUtc)
    {
        Id = id;
        CandidateId = candidateId;
        StorageKey = storageKey;
        OriginalFileName = originalFileName;
        ContentType = contentType;
        Size = size;
        Sha256 = sha256;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid CandidateId { get; private set; }
    public string StorageKey { get; private set; } = string.Empty;
    public string OriginalFileName { get; private set; } = string.Empty;
    public string ContentType { get; private set; } = string.Empty;
    public long Size { get; private set; }
    public string Sha256 { get; private set; } = string.Empty;

    /// <summary>
    /// The business kind of the document (CV, cover letter, certificate, ...). Free text:
    /// it is descriptive metadata, not a value the product branches on.
    /// </summary>
    public string DocumentType { get; private set; } = string.Empty;

    /// <summary>
    /// Marks the candidate's principal document. At most one document per candidate may
    /// carry this, enforced by a partial unique index rather than by writer discipline.
    /// </summary>
    public bool IsPrimary { get; private set; }

    /// <summary>
    /// Provenance of a document loaded from the legacy Access dataset; null for documents
    /// the application created. See <see cref="Candidates.Candidate.SourceKey"/>.
    /// </summary>
    public string? SourceKey { get; private set; }

    public DocumentScanState ScanState { get; private set; } = DocumentScanState.PendingScan;
    public string? ScanFailureCode { get; private set; }
    public string? ScannerSignature { get; private set; }
    public DateTimeOffset? ScannedAtUtc { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public uint Version { get; private set; }

    public void SetDocumentType(string? documentType) =>
        DocumentType = (documentType ?? string.Empty).Trim();

    public void SetPrimary(bool isPrimary, DateTimeOffset updatedAtUtc)
    {
        if (IsPrimary == isPrimary)
        {
            return;
        }
        IsPrimary = isPrimary;
        UpdatedAtUtc = updatedAtUtc;
    }

    public void SetSourceKey(string? sourceKey) =>
        SourceKey = string.IsNullOrWhiteSpace(sourceKey) ? null : sourceKey.Trim();

    public void MarkClean(string? signature, DateTimeOffset scannedAtUtc)
    {
        if (ScanState == DocumentScanState.Clean)
        {
            return;
        }
        if (ScanState != DocumentScanState.PendingScan)
        {
            throw new InvalidOperationException("The document cannot transition to clean.");
        }
        ScanState = DocumentScanState.Clean;
        ScannerSignature = signature;
        ScannedAtUtc = scannedAtUtc;
        ScanFailureCode = null;
        UpdatedAtUtc = scannedAtUtc;
    }

    public void MarkUnavailable(DocumentScanState state, string failureCode, DateTimeOffset scannedAtUtc)
    {
        if (state is not (DocumentScanState.Infected or DocumentScanState.Rejected or DocumentScanState.ScanFailed))
        {
            throw new ArgumentOutOfRangeException(nameof(state));
        }
        ScanState = state;
        ScanFailureCode = failureCode;
        ScannedAtUtc = scannedAtUtc;
        UpdatedAtUtc = scannedAtUtc;
    }
}
