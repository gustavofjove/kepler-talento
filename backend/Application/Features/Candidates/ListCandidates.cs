using KeplerTalento.Application.Abstractions.Identity;
using KeplerTalento.Application.Abstractions.Persistence;
using MediatR;

namespace KeplerTalento.Application.Features.Candidates;

public sealed record ListCandidatesQuery(bool IncludeInactive) : IRequest<IReadOnlyList<CandidateSummaryResponse>>;

public sealed class ListCandidatesHandler(ICandidateRepository candidates, ICurrentActor actor)
    : IRequestHandler<ListCandidatesQuery, IReadOnlyList<CandidateSummaryResponse>>
{
    /// <summary>
    /// Returns the summary projection — core fields, no collections. Logically removed
    /// candidates are excluded unless the caller explicitly asks for them.
    /// </summary>
    public async Task<IReadOnlyList<CandidateSummaryResponse>> Handle(
        ListCandidatesQuery request,
        CancellationToken cancellationToken)
    {
        CandidateGuards.RequireRead(actor);
        var summaries = await candidates.ListAsync(request.IncludeInactive, cancellationToken);
        return [.. summaries.Select(CandidateSummaryResponse.From)];
    }
}
