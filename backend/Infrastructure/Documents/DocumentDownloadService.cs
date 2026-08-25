using KeplerTalento.Application.Abstractions.Documents;
using KeplerTalento.Domain.Documents;
using KeplerTalento.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace KeplerTalento.Infrastructure.Documents;

public sealed class DocumentDownloadService(ApplicationDbContext dbContext, IDocumentStorage storage) : IDocumentDownloadService
{
    public async Task<DocumentDownload?> OpenCleanAsync(Guid documentId, CancellationToken cancellationToken)
    {
        var metadata = await dbContext.Documents.AsNoTracking().SingleOrDefaultAsync(value => value.Id == documentId, cancellationToken);
        if (metadata is null || metadata.ScanState != DocumentScanState.Clean || !await storage.AvailableExistsAsync(metadata.StorageKey, cancellationToken))
        {
            return null;
        }
        return new(
            await storage.OpenAvailableAsync(metadata.StorageKey, cancellationToken),
            metadata.ContentType,
            SanitizeFileName(metadata.OriginalFileName));
    }

    public static string SanitizeFileName(string value)
    {
        var sanitized = string.Concat(value.Select(character =>
            character is '/' or '\\' || char.IsControl(character) ? '_' : character));
        sanitized = sanitized.Trim().TrimStart('.');
        return string.IsNullOrWhiteSpace(sanitized) ? "documento" : sanitized[..Math.Min(180, sanitized.Length)];
    }
}
