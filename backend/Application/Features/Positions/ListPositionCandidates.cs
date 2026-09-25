using KeplerTalento.Application.Abstractions.Identity;
using KeplerTalento.Application.Abstractions.Persistence;
using KeplerTalento.Application.Common.Errors;
using MediatR;

namespace KeplerTalento.Application.Features.Positions;

/// <summary>
/// Every link of one position (KTL-30). Unpaged on purpose: a position holds at most
/// <see cref="Domain.Positions.PositionCandidate.MaximumPerPosition"/> links, and the matches
/// view needs the complete set to show which candidates are already added.
/// </summary>
public sealed record ListPositionCandidatesQuery(Guid PositionId) : IRequest<IReadOnlyList<PositionCandidateResponse>>;

public sealed class ListPositionCandidatesHandler(IPositionRepository positions, ICurrentActor actor)
    : IRequestHandler<ListPositionCandidatesQuery, IReadOnlyList<PositionCandidateResponse>>
{
    public async Task<IReadOnlyList<PositionCandidateResponse>> Handle(ListPositionCandidatesQuery request, CancellationToken cancellationToken)
    {
        PositionGuards.RequireReadCandidates(actor);
        _ = await positions.IsPositionOpenAsync(request.PositionId, cancellationToken)
            ?? throw new NotFoundException(PositionErrors.NotFound, "Posición no encontrada.");
        var items = await positions.ListCandidatesAsync(request.PositionId, cancellationToken);
        return items.Select(item => item.ToResponse()).ToList();
    }
}
