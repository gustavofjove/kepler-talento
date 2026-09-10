using KeplerTalento.Application.Abstractions.Identity;
using KeplerTalento.Application.Abstractions.Persistence;
using MediatR;

namespace KeplerTalento.Application.Features.Candidates;

public sealed record GetCandidateQuery(Guid Id) : IRequest<CandidateResponse>;

public sealed class GetCandidateHandler(
    ICandidateRepository candidates,
    ICatalogRepository catalogs,
    ICurrentActor actor)
    : IRequestHandler<GetCandidateQuery, CandidateResponse>
{
    /// <summary>
    /// Returns the complete aggregate. Reading by identifier deliberately does not filter
    /// on the active state: a logically removed candidate must stay retrievable, and the
    /// detail screen renders one with a restore action.
    /// </summary>
    public async Task<CandidateResponse> Handle(
        GetCandidateQuery request,
        CancellationToken cancellationToken)
    {
        CandidateGuards.RequireRead(actor);
        var candidate = await candidates.FindAsync(request.Id, cancellationToken)
            ?? throw CandidateGuards.NotFound();
        var documents = await candidates.ListDocumentsAsync(request.Id, cancellationToken);
        return await CandidateProjection.ToResponseAsync(
            candidate,
            documents,
            new CandidateCatalogLookup(catalogs),
            cancellationToken);
    }
}
