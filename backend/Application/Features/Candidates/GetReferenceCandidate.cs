using FluentValidation;
using KeplerTalento.Application.Abstractions.Identity;
using KeplerTalento.Application.Abstractions.Persistence;
using KeplerTalento.Application.Common.Errors;
using MediatR;

namespace KeplerTalento.Application.Features.Candidates;

public sealed record GetReferenceCandidateQuery(Guid Id) : IRequest<ReferenceCandidateResponse>;

public sealed record ReferenceCandidateResponse(Guid Id, string FirstName, string LastName, bool IsActive);

public sealed class GetReferenceCandidateValidator : AbstractValidator<GetReferenceCandidateQuery>
{
    public GetReferenceCandidateValidator()
    {
        RuleFor(query => query.Id)
            .NotEmpty()
            .WithErrorCode("candidate.id.required")
            .WithMessage("El identificador del candidato es obligatorio.");
    }
}

public sealed class GetReferenceCandidateHandler(ICandidateReader reader, ICurrentActor actor)
    : IRequestHandler<GetReferenceCandidateQuery, ReferenceCandidateResponse>
{
    public async Task<ReferenceCandidateResponse> Handle(
        GetReferenceCandidateQuery request,
        CancellationToken cancellationToken)
    {
        if (!actor.IsAuthenticated || !actor.HasPermission(Permissions.CandidatesRead))
        {
            throw new ForbiddenException();
        }
        var candidate = await reader.FindAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException("candidate.not_found", "No se ha encontrado el candidato.");
        return new(candidate.Id, candidate.FirstName, candidate.LastName, candidate.IsActive);
    }
}
