using KeplerTalento.Application.Abstractions.Identity;
using KeplerTalento.Application.Abstractions.Persistence;
using KeplerTalento.Domain.Candidates;
using MediatR;

namespace KeplerTalento.Application.Features.Candidates;

public sealed record SetCandidateActiveCommand(Guid Id, bool IsActive, uint Version)
    : IRequest<CandidateResponse>;

/// <summary>
/// Logical removal and restoration, as one state transition rather than two verbs.
/// </summary>
/// <remarks>
/// Both directions require <c>candidates.delete</c>: restoring a removed record is as
/// consequential as removing it, so it is not governed by the lesser update capability.
/// Removal advances the row version like any other write, which is what makes an editor
/// holding a pre-removal token get a conflict rather than resurrect the candidate.
/// </remarks>
public sealed class SetCandidateActiveHandler(
    ICandidateRepository candidates,
    ICatalogRepository catalogs,
    ICurrentActor actor)
    : IRequestHandler<SetCandidateActiveCommand, CandidateResponse>
{
    public async Task<CandidateResponse> Handle(
        SetCandidateActiveCommand request,
        CancellationToken cancellationToken)
    {
        CandidateGuards.RequireDelete(actor);
        var candidate = await candidates.FindAsync(request.Id, cancellationToken)
            ?? throw CandidateGuards.NotFound();

        // A candidate already in the requested state is left entirely alone — no write,
        // no version bump, and above all no audit event, which must describe a change
        // that actually happened rather than a request that arrived.
        if (candidate.IsActive != request.IsActive)
        {
            var now = DateTimeOffset.UtcNow;
            candidates.ExpectVersion(candidate, request.Version);
            if (request.IsActive)
            {
                candidate.Reactivate(now);
            }
            else
            {
                candidate.Deactivate(now);
            }

            var outcome = await candidates.SaveAsync(
                request.IsActive ? CandidateAuditEvents.Restored : CandidateAuditEvents.Removed,
                candidate.Id.ToString("N"),
                cancellationToken);
            if (outcome != CandidateSaveOutcome.Saved)
            {
                throw CandidateGuards.ToException(outcome);
            }
        }

        var documents = await candidates.ListDocumentsAsync(candidate.Id, cancellationToken);
        return await CandidateProjection.ToResponseAsync(
            candidate,
            documents,
            new CandidateCatalogLookup(catalogs),
            cancellationToken);
    }
}
