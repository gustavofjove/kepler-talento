using KeplerTalento.Application.Abstractions.Identity;
using KeplerTalento.Application.Abstractions.Persistence;
using KeplerTalento.Domain.Candidates;
using MediatR;

namespace KeplerTalento.Application.Features.Candidates;

public sealed record CandidateDocumentInput(
    Guid? Id,
    string DocumentType,
    string OriginalFilename,
    string MimeType,
    long SizeBytes,
    bool IsPrimary);

public sealed record SetCandidateDocumentsCommand(
    Guid Id,
    IReadOnlyList<CandidateDocumentInput> Documents,
    uint Version) : IRequest<CandidateResponse>;

/// <summary>
/// Document <em>metadata</em> writes. No file bytes are accepted, stored or served here:
/// upload, scanning, quarantine and secure download are KTL-9.
/// </summary>
public sealed class SetCandidateDocumentsHandler(
    ICandidateRepository candidates,
    ICatalogRepository catalogs,
    ICurrentActor actor)
    : IRequestHandler<SetCandidateDocumentsCommand, CandidateResponse>
{
    public async Task<CandidateResponse> Handle(
        SetCandidateDocumentsCommand request,
        CancellationToken cancellationToken)
    {
        CandidateGuards.RequireUpdate(actor);
        var candidate = await candidates.FindAsync(request.Id, cancellationToken)
            ?? throw CandidateGuards.NotFound();
        if (!candidate.IsActive)
        {
            throw CandidateGuards.Removed();
        }
        // Refused here as well as by the partial unique index, so the caller gets a stable
        // code and a Spanish message rather than a constraint failure.
        if (request.Documents.Count(document => document.IsPrimary) > 1)
        {
            throw CandidateGuards.PrimaryAmbiguous();
        }

        var outcome = await candidates.ReplaceDocumentsAsync(
            candidate,
            request.Version,
            [.. request.Documents.Select(document => new DocumentMetadata(
                document.Id,
                document.DocumentType,
                document.OriginalFilename,
                document.MimeType,
                document.SizeBytes,
                document.IsPrimary))],
            CandidateAuditEvents.DocumentsChanged,
            cancellationToken);
        if (outcome != CandidateSaveOutcome.Saved)
        {
            throw CandidateGuards.ToException(outcome);
        }
        return await CandidateProjection.ReloadAsync(
            candidates,
            new CandidateCatalogLookup(catalogs),
            candidate.Id,
            cancellationToken);
    }
}
