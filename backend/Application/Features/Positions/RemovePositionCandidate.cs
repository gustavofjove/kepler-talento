using KeplerTalento.Application.Abstractions.Identity;
using KeplerTalento.Application.Abstractions.Persistence;
using KeplerTalento.Domain.Positions;
using MediatR;

namespace KeplerTalento.Application.Features.Positions;

/// <summary>
/// Deletes one link permanently, to correct a mistaken addition (KTL-30). Rejecting a candidate
/// is the <c>rejected</c> stage instead; only the audit event outlives a removal.
/// </summary>
public sealed record RemovePositionCandidateCommand(Guid PositionId, Guid CandidateId) : IRequest;

public sealed class RemovePositionCandidateHandler(IPositionRepository positions, ICurrentActor actor)
    : IRequestHandler<RemovePositionCandidateCommand>
{
    public async Task Handle(RemovePositionCandidateCommand request, CancellationToken cancellationToken)
    {
        PositionGuards.RequireManageCandidates(actor);
        await PositionCandidateChecks.RequireOpenPosition(positions, request.PositionId, cancellationToken);
        var link = await PositionCandidateChecks.RequireLink(positions, request.PositionId, request.CandidateId, cancellationToken);
        positions.RemoveLink(link);
        await PositionCandidateChecks.Save(positions, PositionAuditEvents.CandidateRemoved, link, cancellationToken);
    }
}
