using System.Security.Cryptography;
using KeplerTalento.Application.Abstractions.Documents;

namespace KeplerTalento.Infrastructure.Documents;

public sealed class FileSystemDocumentStorage(DocumentStorageOptions options) : IDocumentStorage, IDocumentStorageInventory
{
    private readonly string _quarantineRoot = PrepareRoot(options, options.QuarantineDirectory);
    private readonly string _availableRoot = PrepareRoot(options, options.AvailableDirectory);

    public async Task<StoredFile> WriteQuarantineAsync(
        string storageKey,
        Stream content,
        long maximumBytes,
        CancellationToken cancellationToken)
    {
        var path = DocumentStorageKey.ResolveContained(_quarantineRoot, storageKey);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        long total = 0;
        var created = false;
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        try
        {
            await using var target = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous | FileOptions.WriteThrough);
            created = true;
            var buffer = new byte[81920];
            int read;
            while ((read = await content.ReadAsync(buffer, cancellationToken)) > 0)
            {
                total += read;
                if (total > maximumBytes) throw new InvalidOperationException("document.size.exceeded");
                hash.AppendData(buffer, 0, read);
                await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            }
            if (total == 0) throw new InvalidOperationException("document.empty");
            await target.FlushAsync(cancellationToken);
            return new(storageKey, total, Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant());
        }
        catch
        {
            if (created && File.Exists(path)) File.Delete(path);
            throw;
        }
    }

    public Task<Stream> OpenQuarantineAsync(string storageKey, CancellationToken cancellationToken) =>
        Task.FromResult<Stream>(OpenRead(DocumentStorageKey.ResolveContained(_quarantineRoot, storageKey)));

    public Task<Stream> OpenAvailableAsync(string storageKey, CancellationToken cancellationToken) =>
        Task.FromResult<Stream>(OpenRead(DocumentStorageKey.ResolveContained(_availableRoot, storageKey)));

    public Task<bool> AvailableExistsAsync(string storageKey, CancellationToken cancellationToken) =>
        Task.FromResult(File.Exists(DocumentStorageKey.ResolveContained(_availableRoot, storageKey)));

    public Task PromoteAsync(string storageKey, CancellationToken cancellationToken)
    {
        var source = DocumentStorageKey.ResolveContained(_quarantineRoot, storageKey);
        var destination = DocumentStorageKey.ResolveContained(_availableRoot, storageKey);
        if (File.Exists(destination)) return Task.CompletedTask;
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        File.Move(source, destination, overwrite: false);
        return Task.CompletedTask;
    }

    public Task DeleteQuarantineIfExistsAsync(string storageKey, CancellationToken cancellationToken)
    {
        var path = DocumentStorageKey.ResolveContained(_quarantineRoot, storageKey);
        if (File.Exists(path)) File.Delete(path);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<StoredDocumentObject>> ListAvailableAsync(CancellationToken cancellationToken) =>
        ListAsync(_availableRoot, cancellationToken);

    public Task<IReadOnlyList<StoredDocumentObject>> ListQuarantineAsync(CancellationToken cancellationToken) =>
        ListAsync(_quarantineRoot, cancellationToken);

    public Task<string> ComputeAvailableSha256Async(string storageKey, CancellationToken cancellationToken) =>
        ComputeSha256Async(_availableRoot, storageKey, cancellationToken);

    public Task<string> ComputeQuarantineSha256Async(string storageKey, CancellationToken cancellationToken) =>
        ComputeSha256Async(_quarantineRoot, storageKey, cancellationToken);

    public static void ValidateAndPrepare(DocumentStorageOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.Root) || options.MaximumBytes is <= 0 or > DocumentStorageOptions.AbsoluteMaximumBytes || options.ScanTimeoutSeconds is < 1 or > 600)
        {
            throw new InvalidOperationException("Document storage configuration is unsafe.");
        }
        Probe(PrepareRoot(options, options.QuarantineDirectory));
        Probe(PrepareRoot(options, options.AvailableDirectory));
    }

    private static string PrepareRoot(DocumentStorageOptions options, string child)
    {
        if (Path.IsPathRooted(child) || child.Contains("..", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Document storage child directories must be relative.");
        }
        var path = Path.GetFullPath(Path.Combine(options.Root, child));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void Probe(string root)
    {
        var probe = Path.Combine(root, $".write-probe-{Guid.NewGuid():N}");
        using (File.Create(probe)) { }
        File.Delete(probe);
    }

    private static Task<IReadOnlyList<StoredDocumentObject>> ListAsync(string root, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<StoredDocumentObject> objects = Directory
            .EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Select(path => new StoredDocumentObject(
                Path.GetRelativePath(root, path).Replace(Path.DirectorySeparatorChar, '/'),
                File.GetLastWriteTimeUtc(path)))
            .ToList();
        return Task.FromResult(objects);
    }

    private static async Task<string> ComputeSha256Async(
        string root,
        string storageKey,
        CancellationToken cancellationToken)
    {
        await using var content = OpenRead(DocumentStorageKey.ResolveContained(root, storageKey));
        var hash = await SHA256.HashDataAsync(content, cancellationToken);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static FileStream OpenRead(string path) => new(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.Asynchronous | FileOptions.SequentialScan);
}
