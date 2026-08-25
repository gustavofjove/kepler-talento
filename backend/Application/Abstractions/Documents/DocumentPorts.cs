namespace KeplerTalento.Application.Abstractions.Documents;

public sealed record StoredFile(string StorageKey, long Size, string Sha256);
public sealed record StoredDocumentObject(string StorageKey, DateTimeOffset LastModifiedAtUtc);
public sealed record InspectedDocument(bool Accepted, string Code, string ContentType);
public sealed record ScanResult(ScanVerdict Verdict, string Code, string? Signature = null);

public enum ScanVerdict
{
    Clean,
    Infected,
    Error,
}

public interface IDocumentStorage
{
    Task<StoredFile> WriteQuarantineAsync(string storageKey, Stream content, long maximumBytes, CancellationToken cancellationToken);
    Task<Stream> OpenQuarantineAsync(string storageKey, CancellationToken cancellationToken);
    Task PromoteAsync(string storageKey, CancellationToken cancellationToken);
    Task<Stream> OpenAvailableAsync(string storageKey, CancellationToken cancellationToken);
    Task<bool> AvailableExistsAsync(string storageKey, CancellationToken cancellationToken);
    Task DeleteQuarantineIfExistsAsync(string storageKey, CancellationToken cancellationToken);
}

public interface IDocumentStorageInventory
{
    Task<IReadOnlyList<StoredDocumentObject>> ListAvailableAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<StoredDocumentObject>> ListQuarantineAsync(CancellationToken cancellationToken);
    Task<string> ComputeAvailableSha256Async(string storageKey, CancellationToken cancellationToken);
    Task<string> ComputeQuarantineSha256Async(string storageKey, CancellationToken cancellationToken);
}

public interface IDocumentContentInspector
{
    Task<InspectedDocument> InspectAsync(string fileName, Stream content, CancellationToken cancellationToken);
}

public interface IMalwareScanner
{
    Task<ScanResult> ScanAsync(Stream content, CancellationToken cancellationToken);
}

public sealed record DocumentDownload(Stream Content, string ContentType, string FileName);

public interface IDocumentDownloadService
{
    Task<DocumentDownload?> OpenCleanAsync(Guid documentId, CancellationToken cancellationToken);
}
