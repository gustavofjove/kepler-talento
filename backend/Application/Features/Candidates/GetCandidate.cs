using KeplerTalento.Application.Abstractions.Correlation;
using KeplerTalento.Application.Abstractions.Identity;
using KeplerTalento.Application.Abstractions.Persistence;
using KeplerTalento.Domain.Auditing;
using KeplerTalento.Domain.Candidates;
using MediatR;

namespace KeplerTalento.Application.Features.Candidates;

public sealed record GetCandidateQuery(Guid Id) : IRequest<CandidateResponse>;

public sealed class GetCandidateHandler(
    ICandidateRepository candidates,
    ICatalogRepository catalogs,
    IAuditRepository audits,
    ICorrelationContext correlation,
    ICurrentActor actor)
    : IRequestHandler<GetCandidateQuery, CandidateResponse>
{
    /// <summary>
    /// Returns the complete aggregate. Reading by identifier deliberately does not filter
    /// on the active state: a logically removed candidate must stay retrievable, and the
    /// detail screen renders one with a restore action.
    /// </summary>
    /// <remarks>
    /// Opening one candidate names an individual, so the read is audited — here, after the read
    /// has succeeded, because there is no write transaction for a repository to join. A refused
    /// or not-found read records nothing (KTL-19 design D4, D5).
    /// </remarks>
    public async Task<CandidateResponse> Handle(
        GetCandidateQuery request,
        CancellationToken cancellationToken)
    {
        CandidateGuards.RequireRead(actor);
        var auditActor = actor.ToAuditActor();
        var candidate = await candidates.FindAsync(request.Id, cancellationToken)
            ?? throw CandidateGuards.NotFound();
        var documents = await candidates.ListDocumentsAsync(request.Id, cancellationToken);
        var response = await CandidateProjection.ToResponseAsync(
            candidate,
            documents,
            new CandidateCatalogLookup(catalogs),
            cancellationToken);
        await audits.RecordAsync(
            new AuditEvent(
                Guid.CreateVersion7(),
                CandidateAuditEvents.Read,
                candidate.Id.ToString("N"),
                correlation.CorrelationId,
                DateTimeOffset.UtcNow,
                auditActor,
                "served"),
            cancellationToken);
        return response;
    }
}
