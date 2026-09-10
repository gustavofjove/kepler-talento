namespace KeplerTalento.Infrastructure.Documents;

public sealed class DocumentStorageOptions
{
    public const string SectionName = "DocumentStorage";
    public const long AbsoluteMaximumBytes = 20 * 1024 * 1024;
    public const long MultipartEnvelopeBytes = 1024 * 1024;
    public string Root { get; init; } = string.Empty;
    public string QuarantineDirectory { get; init; } = "quarantine";
    public string AvailableDirectory { get; init; } = "available";
    public long MaximumBytes { get; init; } = AbsoluteMaximumBytes;
    public long MaximumRequestBodyBytes => MaximumBytes + MultipartEnvelopeBytes;
    public int ScanTimeoutSeconds { get; init; } = 60;
}
