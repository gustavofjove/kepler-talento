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
    public DocumentScanState ScanState { get; private set; } = DocumentScanState.PendingScan;
    public string? ScanFailureCode { get; private set; }
    public string? ScannerSignature { get; private set; }
    public DateTimeOffset? ScannedAtUtc { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public uint Version { get; private set; }

    public void MarkClean(string? signature, DateTimeOffset scannedAtUtc)
    {
        if (ScanState == DocumentScanState.Clean)
        {
            return;
        }
        if (ScanState != DocumentScanState.PendingScan && ScanState != DocumentScanState.ScanFailed)
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
