using KeplerTalento.Application.Abstractions.Correlation;
using KeplerTalento.Application.Abstractions.Documents;
using KeplerTalento.Application.Abstractions.Identity;
using KeplerTalento.Application.Abstractions.Operations;
using KeplerTalento.Domain.Documents;
using MediatR;

namespace KeplerTalento.Application.Features.Documents;

public sealed record UploadCandidateDocumentCommand(
    Guid CandidateId,
    string OriginalFileName,
    string DocumentType,
    bool IsPrimary,
    Stream Content,
    long MaximumBytes) : IRequest<CandidateDocumentResponse>;

public sealed class UploadCandidateDocumentHandler(
    IDocumentRepository documents,
    IDocumentStorage storage,
    IDocumentStorageKeyFactory storageKeys,
    IDocumentContentInspector inspector,
    IOperationRepository operations,
    ICorrelationContext correlation,
    ICurrentActor actor) : IRequestHandler<UploadCandidateDocumentCommand, CandidateDocumentResponse>
{
    public async Task<CandidateDocumentResponse> Handle(
        UploadCandidateDocumentCommand request,
        CancellationToken cancellationToken)
    {
        DocumentErrors.Require(actor, Permissions.DocumentsUpload);
        if (!await documents.CandidateExistsAsync(request.CandidateId, cancellationToken))
        {
            throw DocumentErrors.Missing();
        }

        var documentId = Guid.CreateVersion7();
        var storageKey = storageKeys.Create(request.CandidateId, documentId);
        StoredFile stored;
        try
        {
            stored = await storage.WriteQuarantineAsync(
                storageKey,
                request.Content,
                request.MaximumBytes,
                cancellationToken);
        }
        catch (InvalidOperationException exception) when (exception.Message == "document.size.exceeded")
        {
            throw DocumentErrors.Validation(exception.Message, "El archivo supera el máximo permitido de 20 MB.");
        }
        catch (InvalidOperationException exception) when (exception.Message == "document.empty")
        {
            throw DocumentErrors.Validation(exception.Message, "El archivo está vacío.");
        }

        try
        {
            InspectedDocument inspection;
            await using (var quarantined = await storage.OpenQuarantineAsync(storageKey, cancellationToken))
            {
                inspection = await inspector.InspectAsync(request.OriginalFileName, quarantined, cancellationToken);
            }
            if (!inspection.Accepted)
            {
                await storage.DeleteQuarantineIfExistsAsync(storageKey, cancellationToken);
                throw DocumentErrors.Validation(
                    inspection.Code,
                    "El tipo o el contenido del archivo no está permitido.");
            }

            var now = DateTimeOffset.UtcNow;
            if (request.IsPrimary)
            {
                await documents.ClearPrimaryAsync(request.CandidateId, now, cancellationToken);
            }
            var document = new CandidateDocument(
                documentId,
                request.CandidateId,
                storageKey,
                request.OriginalFileName,
                inspection.ContentType,
                stored.Size,
                stored.Sha256,
                now);
            document.SetDocumentType(request.DocumentType);
            document.SetPrimary(request.IsPrimary, now);
            documents.Add(document);
            documents.AddAudit(
                "document.upload.accepted",
                request.CandidateId,
                documentId,
                "accepted",
                correlation.CorrelationId,
                actor.ExternalKey);
            await operations.EnqueueAsync(
                "document.scan",
                correlation.CorrelationId,
                $"document:{documentId}:scan",
                cancellationToken);
            return CandidateDocumentResponse.From(document);
        }
        catch
        {
            await storage.DeleteQuarantineIfExistsAsync(storageKey, CancellationToken.None);
            throw;
        }
    }
}
