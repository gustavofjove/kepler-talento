using FluentValidation;
using KeplerTalento.Application.Abstractions.Identity;
using KeplerTalento.Application.Abstractions.Persistence;
using KeplerTalento.Application.Common.Errors;
using KeplerTalento.Domain.Positions;
using MediatR;

namespace KeplerTalento.Application.Features.Positions;

/// <summary>Moves one link to another stage (KTL-30). The candidate's own status is untouched.</summary>
public sealed record ChangePositionCandidateStageCommand(Guid PositionId, Guid CandidateId, string? Stage, uint Version)
    : IRequest<PositionCandidateResponse>;

public sealed class ChangePositionCandidateStageValidator : AbstractValidator<ChangePositionCandidateStageCommand>
{
    public ChangePositionCandidateStageValidator()
    {
        RuleFor(x => x.Stage).Must(PositionCandidateStages.IsKnown).WithErrorCode(PositionErrors.CandidateStageInvalid);
        RuleFor(x => x.Version).GreaterThan(0u).WithErrorCode(PositionErrors.VersionInvalid);
    }
}

public sealed class ChangePositionCandidateStageHandler(IPositionRepository positions, ICurrentActor actor)
    : IRequestHandler<ChangePositionCandidateStageCommand, PositionCandidateResponse>
{
    public async Task<PositionCandidateResponse> Handle(ChangePositionCandidateStageCommand request, CancellationToken cancellationToken)
    {
        PositionGuards.RequireManageCandidates(actor);
        if (!PositionCandidateStages.IsKnown(request.Stage))
            throw new RequestValidationException([new("Stage", PositionErrors.CandidateStageInvalid, "El estado no es válido.")]);
        await PositionCandidateChecks.RequireOpenPosition(positions, request.PositionId, cancellationToken);
        var link = await PositionCandidateChecks.RequireLink(positions, request.PositionId, request.CandidateId, cancellationToken);
        positions.ExpectLinkVersion(link, request.Version);
        link.ChangeStage(request.Stage!, DateTimeOffset.UtcNow);
        await PositionCandidateChecks.Save(positions, PositionAuditEvents.CandidateStageChanged, link, cancellationToken);
        var changed = await positions.FindCandidateItemAsync(request.PositionId, request.CandidateId, cancellationToken)
            ?? throw new NotFoundException(PositionErrors.CandidateLinkNotFound, "El candidato no está en esta posición.");
        return changed.ToResponse();
    }
}
