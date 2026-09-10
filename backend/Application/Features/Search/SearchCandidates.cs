using KeplerTalento.Application.Abstractions.Identity;
using KeplerTalento.Application.Abstractions.Persistence;
using KeplerTalento.Application.Common.Errors;
using MediatR;

namespace KeplerTalento.Application.Features.Search;

/// <summary>
/// One page of a candidate search.
/// </summary>
/// <remarks>
/// Absent pagination means the documented defaults; out-of-range pagination is a refusal.
/// The two are not the same: omitting a page size accepts what the server offers, while
/// asking for a thousand rows is a request it will not serve and does not quietly rewrite
/// into one it will.
/// </remarks>
public sealed record SearchCandidatesQuery(SearchFiltersInput? Filters, int? Page, int? PageSize)
    : IRequest<SearchPage<CandidateSearchItem>>;

public sealed class SearchCandidatesHandler(ICandidateRepository candidates, ICurrentActor actor)
    : IRequestHandler<SearchCandidatesQuery, SearchPage<CandidateSearchItem>>
{
    public async Task<SearchPage<CandidateSearchItem>> Handle(
        SearchCandidatesQuery request,
        CancellationToken cancellationToken)
    {
        // Authorization first, before validation and before the repository is touched: an
        // unauthorized caller must not be able to tell a well-formed request from a
        // malformed one, let alone reach the data.
        SearchGuards.RequireRead(actor);

        // Validation is collected rather than thrown at the first problem, so one refusal
        // reports everything wrong with the request.
        var issues = new List<ValidationIssue>();
        var filters = SearchFilterNormalization.TryNormalize(request.Filters, "Filters", issues);
        var (page, pageSize) = SearchPaging.Validate(request.Page, request.PageSize, issues);
        if (issues.Count > 0)
        {
            throw new RequestValidationException(issues);
        }

        return await candidates.SearchAsync(filters, page, pageSize, cancellationToken);
    }
}
