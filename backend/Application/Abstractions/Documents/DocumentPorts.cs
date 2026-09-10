using KeplerTalento.Domain.Documents;

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
    Task DeleteAvailableIfExistsAsync(string storageKey, CancellationToken cancellationToken);
}

public interface IDocumentStorageKeyFactory
{
    string Create(Guid candidateId, Guid documentId);
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

public enum DocumentSaveOutcome
{
    Saved,
    NotFound,
    Conflict,
}

public interface IDocumentRepository
{
    Task<bool> CandidateExistsAsync(Guid candidateId, CancellationToken cancellationToken);
    Task<CandidateDocument?> FindAsync(Guid candidateId, Guid documentId, CancellationToken cancellationToken);
    Task<IReadOnlyList<CandidateDocument>> ListAsync(Guid candidateId, CancellationToken cancellationToken);
    Task ClearPrimaryAsync(Guid candidateId, DateTimeOffset now, CancellationToken cancellationToken);
    void Add(CandidateDocument document);
    void AddAudit(string eventType, Guid candidateId, Guid documentId, string outcome, string correlationId, string? actorExternalKey);
    Task<DocumentSaveOutcome> SetPrimaryAsync(Guid candidateId, Guid documentId, string correlationId, string? actorExternalKey, CancellationToken cancellationToken);
    Task<CandidateDocument?> RemoveAsync(Guid candidateId, Guid documentId, string correlationId, string? actorExternalKey, CancellationToken cancellationToken);
    Task SaveAsync(CancellationToken cancellationToken);
}
