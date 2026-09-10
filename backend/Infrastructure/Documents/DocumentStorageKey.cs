namespace KeplerTalento.Infrastructure.Documents;

public static class DocumentStorageKey
{
    public static string Create(Guid candidateId, Guid documentId) =>
        $"candidates/{candidateId:N}/{documentId:N}/content";

    public static string ResolveContained(string root, string storageKey)
    {
        if (Path.IsPathRooted(storageKey) || storageKey.Contains('\\', StringComparison.Ordinal))
        {
            throw new InvalidOperationException("storage.path.invalid");
        }
        var segments = storageKey.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0 || segments.Any(segment => segment is "." or ".." || segment.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0))
        {
            throw new InvalidOperationException("storage.path.invalid");
        }
        var normalizedRoot = Path.GetFullPath(root);
        var candidate = Path.GetFullPath(Path.Combine([normalizedRoot, .. segments]));
        var prefix = normalizedRoot.EndsWith(Path.DirectorySeparatorChar) ? normalizedRoot : normalizedRoot + Path.DirectorySeparatorChar;
        if (!candidate.StartsWith(prefix, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
        {
            throw new InvalidOperationException("storage.path.invalid");
        }
        return candidate;
    }
}

public sealed class DocumentStorageKeyFactory : KeplerTalento.Application.Abstractions.Documents.IDocumentStorageKeyFactory
{
    public string Create(Guid candidateId, Guid documentId) => DocumentStorageKey.Create(candidateId, documentId);
}
