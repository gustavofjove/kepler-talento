using KeplerTalento.Application.Abstractions.Identity;
using KeplerTalento.Application.Abstractions.Persistence;
using KeplerTalento.Application.Common.Errors;
using KeplerTalento.Application.Features.Candidates;
using MediatR;

namespace KeplerTalento.Application.Features.Positions;

/// <summary>Every position one candidate is linked to, open positions first (KTL-30).</summary>
public sealed record ListCandidatePositionsQuery(Guid CandidateId) : IRequest<IReadOnlyList<CandidatePositionResponse>>;

public sealed class ListCandidatePositionsHandler(IPositionRepository positions, ICurrentActor actor)
    : IRequestHandler<ListCandidatePositionsQuery, IReadOnlyList<CandidatePositionResponse>>
{
    public async Task<IReadOnlyList<CandidatePositionResponse>> Handle(ListCandidatePositionsQuery request, CancellationToken cancellationToken)
    {
        PositionGuards.RequireReadCandidates(actor);
        _ = await positions.IsCandidateActiveAsync(request.CandidateId, cancellationToken)
            ?? throw new NotFoundException(CandidateErrors.NotFound, "Candidato no encontrado.");
        var items = await positions.ListForCandidateAsync(request.CandidateId, cancellationToken);
        return items
            .Select(item => new CandidatePositionResponse(
                item.PositionId, item.Title, item.PositionStatus, item.Stage, item.AddedAtUtc, item.UpdatedAtUtc, item.Version))
            .ToList();
    }
}
