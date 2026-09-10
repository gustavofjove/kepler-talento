using KeplerTalento.Application.Abstractions.Correlation;
using KeplerTalento.Application.Abstractions.Documents;
using KeplerTalento.Application.Abstractions.Identity;
using MediatR;

namespace KeplerTalento.Application.Features.Documents;

public sealed record ListCandidateDocumentsQuery(Guid CandidateId) : IRequest<IReadOnlyList<CandidateDocumentResponse>>;
public sealed record GetCandidateDocumentQuery(Guid CandidateId, Guid DocumentId) : IRequest<CandidateDocumentResponse>;
public sealed record SetPrimaryCandidateDocumentCommand(Guid CandidateId, Guid DocumentId) : IRequest<CandidateDocumentResponse>;
public sealed record RemoveCandidateDocumentCommand(Guid CandidateId, Guid DocumentId) : IRequest;
public sealed record DownloadCandidateDocumentQuery(Guid CandidateId, Guid DocumentId) : IRequest<DocumentDownload>;

public sealed class ListCandidateDocumentsHandler(IDocumentRepository documents, ICurrentActor actor)
    : IRequestHandler<ListCandidateDocumentsQuery, IReadOnlyList<CandidateDocumentResponse>>
{
    public async Task<IReadOnlyList<CandidateDocumentResponse>> Handle(ListCandidateDocumentsQuery request, CancellationToken cancellationToken)
    {
        DocumentErrors.Require(actor, Permissions.CandidatesRead);
        if (!await documents.CandidateExistsAsync(request.CandidateId, cancellationToken)) throw DocumentErrors.Missing();
        return [.. (await documents.ListAsync(request.CandidateId, cancellationToken)).Select(CandidateDocumentResponse.From)];
    }
}

public sealed class GetCandidateDocumentHandler(IDocumentRepository documents, ICurrentActor actor)
    : IRequestHandler<GetCandidateDocumentQuery, CandidateDocumentResponse>
{
    public async Task<CandidateDocumentResponse> Handle(GetCandidateDocumentQuery request, CancellationToken cancellationToken)
    {
        DocumentErrors.Require(actor, Permissions.CandidatesRead);
        var document = await documents.FindAsync(request.CandidateId, request.DocumentId, cancellationToken)
            ?? throw DocumentErrors.Missing();
        return CandidateDocumentResponse.From(document);
    }
}

public sealed class SetPrimaryCandidateDocumentHandler(
    IDocumentRepository documents,
    ICorrelationContext correlation,
    ICurrentActor actor) : IRequestHandler<SetPrimaryCandidateDocumentCommand, CandidateDocumentResponse>
{
    public async Task<CandidateDocumentResponse> Handle(SetPrimaryCandidateDocumentCommand request, CancellationToken cancellationToken)
    {
        DocumentErrors.Require(actor, Permissions.DocumentsUpload);
        var outcome = await documents.SetPrimaryAsync(
            request.CandidateId,
            request.DocumentId,
            correlation.CorrelationId,
            actor.ExternalKey,
            cancellationToken);
        if (outcome == DocumentSaveOutcome.NotFound) throw DocumentErrors.Missing();
        if (outcome == DocumentSaveOutcome.Conflict) throw DocumentErrors.PrimaryConflictException();
        var document = await documents.FindAsync(request.CandidateId, request.DocumentId, cancellationToken)
            ?? throw DocumentErrors.Missing();
        return CandidateDocumentResponse.From(document);
    }
}

public sealed class RemoveCandidateDocumentHandler(
    IDocumentRepository documents,
    IDocumentStorage storage,
    ICorrelationContext correlation,
    ICurrentActor actor) : IRequestHandler<RemoveCandidateDocumentCommand>
{
    public async Task Handle(RemoveCandidateDocumentCommand request, CancellationToken cancellationToken)
    {
        DocumentErrors.Require(actor, Permissions.DocumentsUpload);
        var document = await documents.RemoveAsync(
            request.CandidateId,
            request.DocumentId,
            correlation.CorrelationId,
            actor.ExternalKey,
            cancellationToken) ?? throw DocumentErrors.Missing();

        Exception? deletionFailure = null;
        try { await storage.DeleteAvailableIfExistsAsync(document.StorageKey, cancellationToken); }
        catch (Exception exception) { deletionFailure = exception; }
        try { await storage.DeleteQuarantineIfExistsAsync(document.StorageKey, cancellationToken); }
        catch (Exception exception) { deletionFailure ??= exception; }
        if (deletionFailure is not null) throw deletionFailure;
    }
}

public sealed class DownloadCandidateDocumentHandler(
    IDocumentRepository documents,
    IDocumentDownloadService downloads,
    ICorrelationContext correlation,
    ICurrentActor actor) : IRequestHandler<DownloadCandidateDocumentQuery, DocumentDownload>
{
    public async Task<DocumentDownload> Handle(DownloadCandidateDocumentQuery request, CancellationToken cancellationToken)
    {
        DocumentErrors.Require(actor, Permissions.DocumentsDownload);
        var metadata = await documents.FindAsync(request.CandidateId, request.DocumentId, cancellationToken)
            ?? throw DocumentErrors.Missing();
        var download = await downloads.OpenCleanAsync(metadata.Id, cancellationToken)
            ?? throw DocumentErrors.Missing();
        documents.AddAudit(
            "document.downloaded",
            request.CandidateId,
            request.DocumentId,
            "served",
            correlation.CorrelationId,
            actor.ExternalKey);
        try { await documents.SaveAsync(cancellationToken); }
        catch { await download.Content.DisposeAsync(); throw; }
        return download;
    }
}
