using KeplerTalento.Application.Abstractions.Identity;
using KeplerTalento.Application.Abstractions.Persistence;
using KeplerTalento.Application.Common.Errors;
using MediatR;

namespace KeplerTalento.Application.Features.Positions;

public sealed record GetPositionQuery(Guid Id) : IRequest<PositionResponse>;
public sealed class GetPositionHandler(IPositionRepository positions, ICurrentActor actor)
    : IRequestHandler<GetPositionQuery, PositionResponse>
{
    public async Task<PositionResponse> Handle(GetPositionQuery request, CancellationToken cancellationToken)
    {
        PositionGuards.RequireRead(actor);
        var position = await positions.FindAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException(PositionErrors.NotFound, "Posición no encontrada.");
        return position.ToResponse();
    }
}
