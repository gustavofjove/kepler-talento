using System.Linq.Expressions;
using KeplerTalento.Application.Features.Search;
using KeplerTalento.Domain.Candidates;
using KeplerTalento.Domain.Catalogs;
using Microsoft.EntityFrameworkCore;

namespace KeplerTalento.Infrastructure.Persistence;

/// <summary>
/// Builds the candidate search predicate.
/// </summary>
/// <remarks>
/// The shape matters as much as the result. Everything below is a correlated existence test
/// against the candidate row; no relation is ever joined into the outer query. That is what
/// makes duplicate rows impossible by construction rather than hidden by a <c>DISTINCT</c>
/// — and a <c>DISTINCT</c> covering a join would also make <c>ALL</c> and the total count
/// quietly wrong, which is much harder to notice than a duplicate.
/// </remarks>
public sealed class CandidateSearchQuery(ApplicationDbContext dbContext)
{
    /// <summary>
    /// Catalog identifiers for one criterion. A value nobody has is not an error — it is a
    /// search that matches nothing — so an unresolved criterion yields an empty set and the
    /// predicate built from it is simply false.
    /// </summary>
    private sealed record ResolvedCriterion(Guid[] ValueIds, Guid[] LevelIds, bool MatchesAnyLevel);

    /// <summary>
    /// The candidates a filter value matches. The count and the page are both taken from
    /// this, so neither can drift from the other's idea of "matching".
    /// </summary>
    public async Task<IQueryable<Candidate>> MatchingAsync(SearchFiltersValue filters, CancellationToken cancellationToken)
    {
        // Logical removal is the only removal, so this is the whole of "the candidate exists".
        var query = dbContext.Candidates.AsNoTracking().Where(candidate => candidate.IsActive);

        if (!filters.StatusIsUnrestricted)
        {
            var statuses = filters.StatusValues.ToArray();
            query = query.Where(candidate => statuses.Contains(candidate.Status));
        }

        if (filters.Text.Length > 0)
        {
            // A literal, case-insensitive substring over the same five fields the browser
            // evaluator read. The pattern is escaped, so a name containing % or _ matches
            // those characters instead of behaving as wildcard syntax, and the escape
            // character is declared to PostgreSQL rather than assumed.
            var pattern = SearchTextPattern.Contains(filters.Text);
            var escape = SearchTextPattern.EscapeCharacter.ToString();
            query = query.Where(candidate =>
                EF.Functions.ILike(candidate.FirstName, pattern, escape)
                || EF.Functions.ILike(candidate.LastName, pattern, escape)
                || EF.Functions.ILike(candidate.Email, pattern, escape)
                || EF.Functions.ILike(candidate.Phone, pattern, escape)
                || EF.Functions.ILike(candidate.Notes, pattern, escape));
        }

        query = filters.HasCv switch
        {
            // Presence of a primary record, in any scan state. Whether those bytes can be
            // downloaded is the document capability's decision and is unaffected by this.
            CvPresence.Present => query.Where(candidate =>
                dbContext.Documents.Any(document => document.CandidateId == candidate.Id && document.IsPrimary)),
            CvPresence.Absent => query.Where(candidate =>
                !dbContext.Documents.Any(document => document.CandidateId == candidate.Id && document.IsPrimary)),
            _ => query,
        };

        var catalog = await ResolveCatalogAsync(filters, cancellationToken);

        query = ApplyFamily(
            query,
            filters.SkillCriteria,
            filters.SkillMode,
            criterion => Resolve(catalog, criterion, CatalogFamilies.Skill, CatalogFamilies.SkillLevel),
            SkillPredicate);
        query = ApplyFamily(
            query,
            filters.LanguageCriteria,
            filters.LanguageMode,
            criterion => Resolve(catalog, criterion, CatalogFamilies.Language, CatalogFamilies.LanguageLevel),
            LanguagePredicate);
        query = ApplyFamily(
            query,
            filters.ProgramCriteria,
            filters.ProgramMode,
            criterion => Resolve(catalog, criterion, CatalogFamilies.Program, CatalogFamilies.ProgramLevel),
            ProgramPredicate);

        return query;
    }

    /// <summary>
    /// Orders, pages and projects the matching candidates into search items.
    /// </summary>
    /// <remarks>
    /// This lives beside the predicate rather than in the repository so the query-plan
    /// evidence can capture the statement the application actually issues — a plan for a
    /// reconstruction of the query would prove nothing about the query.
    /// </remarks>
    public IQueryable<CandidateSearchItem> Page(IQueryable<Candidate> matching, int page, int pageSize) =>
        matching
            // Update time descending is the order the screen expects; the identifier breaks
            // ties, without which two candidates saved in the same instant could appear on
            // both of two adjacent pages, or on neither.
            .OrderByDescending(candidate => candidate.UpdatedAtUtc)
            .ThenBy(candidate => candidate.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(candidate => new CandidateSearchItem(
                candidate.Id,
                candidate.FirstName,
                candidate.LastName,
                candidate.Phone,
                candidate.Email,
                candidate.Status,
                dbContext.Documents.Any(document =>
                    document.CandidateId == candidate.Id && document.IsPrimary),
                dbContext.Documents
                    .Where(document => document.CandidateId == candidate.Id && document.IsPrimary)
                    .Select(document => (Guid?)document.Id)
                    .FirstOrDefault(),
                candidate.UpdatedAtUtc));

    /// <summary>
    /// Adds one filter family's predicate.
    /// </summary>
    /// <remarks>
    /// <c>ALL</c> becomes one <c>Where</c> per distinct criterion — separate existence
    /// conditions, so a candidate satisfying two criteria through two different relation
    /// rows matches, which a single-row comparison could never express. <c>ANY</c> becomes
    /// those same conditions combined with <c>OR</c> into one <c>Where</c>.
    /// </remarks>
    private static IQueryable<Candidate> ApplyFamily(
        IQueryable<Candidate> query,
        IReadOnlyList<SearchCriterion> criteria,
        MultiValueMode mode,
        Func<SearchCriterion, ResolvedCriterion> resolve,
        Func<ResolvedCriterion, Expression<Func<Candidate, bool>>> build)
    {
        if (criteria.Count == 0)
        {
            // An empty family restricts nothing and must emit no predicate at all.
            return query;
        }
        var predicates = criteria.Select(criterion => build(resolve(criterion))).ToList();
        if (mode == MultiValueMode.All)
        {
            return predicates.Aggregate(query, (current, predicate) => current.Where(predicate));
        }
        return query.Where(predicates.Aggregate(Or));
    }

    private Expression<Func<Candidate, bool>> SkillPredicate(ResolvedCriterion criterion) =>
        candidate => dbContext.CandidateSkills.Any(relation =>
            relation.CandidateId == candidate.Id
            && criterion.ValueIds.Contains(relation.SkillId)
            && (criterion.MatchesAnyLevel || criterion.LevelIds.Contains(relation.LevelId)));

    private Expression<Func<Candidate, bool>> LanguagePredicate(ResolvedCriterion criterion) =>
        candidate => dbContext.CandidateLanguages.Any(relation =>
            relation.CandidateId == candidate.Id
            && criterion.ValueIds.Contains(relation.LanguageId)
            && (criterion.MatchesAnyLevel || criterion.LevelIds.Contains(relation.LevelId)));

    private Expression<Func<Candidate, bool>> ProgramPredicate(ResolvedCriterion criterion) =>
        candidate => dbContext.CandidatePrograms.Any(relation =>
            relation.CandidateId == candidate.Id
            && criterion.ValueIds.Contains(relation.ProgramId)
            && (criterion.MatchesAnyLevel || criterion.LevelIds.Contains(relation.LevelId)));

    /// <summary>
    /// Resolves every criterion's catalog names to identifiers in one query.
    /// </summary>
    /// <remarks>
    /// Relations reference catalog entries by identifier, not by text, so the comparison
    /// belongs on the catalog row. Doing it once up front turns each criterion into an
    /// identifier comparison against an indexed column, instead of a correlated text join
    /// repeated for every candidate the outer query considers.
    ///
    /// Inactive catalog entries are included deliberately: a value an administrator retired
    /// stays meaningful for the candidates that already hold it, and a search that silently
    /// stopped finding them would be a data-loss bug wearing an administration feature's
    /// clothes.
    /// </remarks>
    private async Task<ILookup<(string Family, string Name), Guid>> ResolveCatalogAsync(
        SearchFiltersValue filters,
        CancellationToken cancellationToken)
    {
        var wanted = new List<(string Family, string Name)>();
        Collect(filters.SkillCriteria, CatalogFamilies.Skill, CatalogFamilies.SkillLevel);
        Collect(filters.LanguageCriteria, CatalogFamilies.Language, CatalogFamilies.LanguageLevel);
        Collect(filters.ProgramCriteria, CatalogFamilies.Program, CatalogFamilies.ProgramLevel);

        void Collect(IReadOnlyList<SearchCriterion> criteria, string valueFamily, string levelFamily)
        {
            foreach (var criterion in criteria)
            {
                wanted.Add((valueFamily, criterion.NormalizedValue));
                if (!criterion.MatchesAnyLevel)
                {
                    wanted.Add((levelFamily, criterion.NormalizedLevel));
                }
            }
        }

        if (wanted.Count == 0)
        {
            return Array.Empty<((string, string) Key, Guid Value)>().ToLookup(pair => pair.Key, pair => pair.Value);
        }
        var families = wanted.Select(item => item.Family).Distinct().ToArray();
        var names = wanted.Select(item => item.Name).Distinct().ToArray();
        var items = await dbContext.CatalogItems
            .AsNoTracking()
            .Where(item => families.Contains(item.Family) && names.Contains(item.NameNormalized))
            .Select(item => new { item.Id, item.Family, item.NameNormalized })
            .ToListAsync(cancellationToken);
        return items.ToLookup(item => (item.Family, item.NameNormalized), item => item.Id);
    }

    private static ResolvedCriterion Resolve(
        ILookup<(string Family, string Name), Guid> catalog,
        SearchCriterion criterion,
        string valueFamily,
        string levelFamily) => new(
        [.. catalog[(valueFamily, criterion.NormalizedValue)]],
        criterion.MatchesAnyLevel ? [] : [.. catalog[(levelFamily, criterion.NormalizedLevel)]],
        criterion.MatchesAnyLevel);

    /// <summary>
    /// Combines two candidate predicates with <c>OR</c> under a single parameter, so the
    /// result is one expression EF can translate rather than two it cannot merge.
    /// </summary>
    private static Expression<Func<Candidate, bool>> Or(
        Expression<Func<Candidate, bool>> left,
        Expression<Func<Candidate, bool>> right)
    {
        var parameter = Expression.Parameter(typeof(Candidate), "candidate");
        return Expression.Lambda<Func<Candidate, bool>>(
            Expression.OrElse(
                new ParameterRebinder(parameter).Visit(left.Body)!,
                new ParameterRebinder(parameter).Visit(right.Body)!),
            parameter);
    }

    private sealed class ParameterRebinder(ParameterExpression parameter) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node) =>
            node.Type == typeof(Candidate) ? parameter : base.VisitParameter(node);
    }
}
