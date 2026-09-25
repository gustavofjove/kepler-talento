using FluentValidation;
using KeplerTalento.Application.Abstractions.Identity;
using KeplerTalento.Application.Abstractions.Persistence;
using KeplerTalento.Application.Common.Errors;
using KeplerTalento.Application.Features.Candidates;
using KeplerTalento.Domain.Positions;
using MediatR;

namespace KeplerTalento.Application.Features.Positions;

/// <summary>
/// Adds a candidate to a position at stage <c>new</c> (KTL-30). The position's requirements are
/// deliberately not evaluated: HR may add someone who applied directly.
/// </summary>
public sealed record AddPositionCandidateCommand(Guid PositionId, Guid? CandidateId) : IRequest<PositionCandidateResponse>;

public sealed class AddPositionCandidateValidator : AbstractValidator<AddPositionCandidateCommand>
{
    public AddPositionCandidateValidator()
    {
        RuleFor(x => x.CandidateId).Must(id => id is { } value && value != Guid.Empty).WithErrorCode(PositionErrors.CandidateRequired);
    }
}

public sealed class AddPositionCandidateHandler(IPositionRepository positions, ICurrentActor actor)
    : IRequestHandler<AddPositionCandidateCommand, PositionCandidateResponse>
{
    public async Task<PositionCandidateResponse> Handle(AddPositionCandidateCommand request, CancellationToken cancellationToken)
    {
        PositionGuards.RequireManageCandidates(actor);
        var candidateId = request.CandidateId ?? Guid.Empty;
        await PositionCandidateChecks.RequireOpenPosition(positions, request.PositionId, cancellationToken);
        var active = await positions.IsCandidateActiveAsync(candidateId, cancellationToken)
            ?? throw new NotFoundException(CandidateErrors.NotFound, "Candidato no encontrado.");
        if (!active) throw new ConflictException(PositionErrors.CandidateInactive, "El candidato está eliminado y no puede añadirse a una posición.");
        if (await positions.FindLinkAsync(request.PositionId, candidateId, cancellationToken) is not null)
            throw new ConflictException(PositionErrors.CandidateAlreadyLinked, "El candidato ya está en esta posición.");
        // Not serializable: two concurrent adds at the limit may both pass (design D4).
        if (await positions.CountLinksAsync(request.PositionId, cancellationToken) >= PositionCandidate.MaximumPerPosition)
            throw new ConflictException(PositionErrors.CandidateLimitReached, "La posición ha alcanzado el máximo de candidatos.");

        var link = new PositionCandidate(Guid.CreateVersion7(), request.PositionId, candidateId, DateTimeOffset.UtcNow);
        positions.AddLink(link);
        await PositionCandidateChecks.Save(positions, PositionAuditEvents.CandidateAdded, link, cancellationToken);
        var added = await positions.FindCandidateItemAsync(request.PositionId, candidateId, cancellationToken)
            ?? throw new NotFoundException(PositionErrors.CandidateLinkNotFound, "El candidato no está en esta posición.");
        return added.ToResponse();
    }
}
