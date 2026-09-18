using KeplerTalento.Application.Abstractions.Correlation;
using KeplerTalento.Application.Abstractions.Identity;
using KeplerTalento.Application.Abstractions.Persistence;
using KeplerTalento.Domain.Auditing;
using KeplerTalento.Domain.Candidates;
using MediatR;

namespace KeplerTalento.Application.Features.Candidates.Notes;

public sealed record ListCandidateNotesQuery(Guid CandidateId)
    : IRequest<IReadOnlyList<CandidateNoteResponse>>;

public sealed class ListCandidateNotesHandler(
    ICandidateRepository candidates,
    IAuditRepository audits,
    ICorrelationContext correlation,
    ICurrentActor actor)
    : IRequestHandler<ListCandidateNotesQuery, IReadOnlyList<CandidateNoteResponse>>
{
    public async Task<IReadOnlyList<CandidateNoteResponse>> Handle(
        ListCandidateNotesQuery request,
        CancellationToken cancellationToken)
    {
        CandidateGuards.RequireRead(actor);
        var candidate = await candidates.FindCoreAsync(request.CandidateId, cancellationToken)
            ?? throw CandidateGuards.NotFound();
        var notes = await candidates.ListNotesAsync(candidate.Id, cancellationToken);
        await audits.RecordAsync(
            new AuditEvent(
                Guid.CreateVersion7(),
                CandidateAuditEvents.Read,
                candidate.Id.ToString("N"),
                correlation.CorrelationId,
                DateTimeOffset.UtcNow,
                actor.ToAuditActor(),
                "notes_served"),
            cancellationToken);
        return [.. notes.Select(CandidateNoteResponse.From)];
    }
}
