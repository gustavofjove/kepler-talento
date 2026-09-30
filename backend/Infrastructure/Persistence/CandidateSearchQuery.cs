using System.Linq.Expressions;
using KeplerTalento.Application.Abstractions.Encryption;
using KeplerTalento.Application.Features.Search;
using KeplerTalento.Infrastructure.Encryption;
using KeplerTalento.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore.Infrastructure;
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
    public async Task<IQueryable<Candidate>> MatchingAsync(
        SearchFiltersValue filters,
        bool includeInactive,
        CancellationToken cancellationToken)
    {
        // Logical removal is the only removal, so this is the whole of "the candidate exists".
        // Including removed candidates is a guarded option (KTL-18); the handler has already
        // checked the permission by the time this is true.
        var query = dbContext.Candidates.AsNoTracking();
        if (!includeInactive)
        {
            query = query.Where(candidate => candidate.IsActive);
        }

        if (!filters.StatusIsUnrestricted)
        {
            var statuses = filters.StatusValues.ToArray();
            query = query.Where(candidate => statuses.Contains(candidate.Status));
        }

        // Free text is not applied here. The five fields it reads are ciphertext since KTL-33, so
        // the match runs in the API over the rows this predicate selects (SearchAsync).

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
        query = ApplyFamily(
            query,
            filters.TagCriteria,
            filters.TagMode,
            criterion => Resolve(catalog, criterion, CatalogFamilies.Tag, CatalogFamilies.Tag),
            TagPredicate);

        return query;
    }

    /// <summary>
    /// Whether a search needs the encrypted fields: a free-text term, or the last-name order.
    /// Everything else stays entirely in SQL.
    /// </summary>
    public static bool NeedsEncryptedStage(SearchFiltersValue filters, SearchOptions options) =>
        filters.Text.Length > 0 || options.Sort.Field == SearchSortField.LastName;

    /// <summary>One page of results and the total, by whichever path the search needs.</summary>
    public async Task<SearchPage<CandidateSearchItem>> SearchAsync(
        SearchFiltersValue filters,
        SearchOptions options,
        CancellationToken cancellationToken)
    {
        var matching = await MatchingAsync(filters, options.IncludeInactive, cancellationToken);
        if (!NeedsEncryptedStage(filters, options))
        {
            var totalCount = await matching.CountAsync(cancellationToken);
            var items = await Page(matching, options).ToListAsync(cancellationToken);
            return new SearchPage<CandidateSearchItem>(items, options.Page, options.PageSize, totalCount);
        }
        return await EncryptedStageAsync(matching, filters.Text, options, cancellationToken);
    }

    /// <summary>
    /// Text match and name order over decrypted values (KTL-33 design decision 6).
    /// </summary>
    /// <remarks>
    /// SQL has already applied every other family, so this reads only the rows that can still
    /// match (<c>"Id" IN (the same predicate)</c>), and only the fields the match and the order
    /// need, as stored envelopes. Rows are decrypted in parallel, and a row's remaining fields are
    /// skipped as soon as one matches. The page's items are then loaded by identifier through the
    /// same projection as the SQL path. Decrypted values live only for this call; nothing is cached.
    /// </remarks>
    private async Task<SearchPage<CandidateSearchItem>> EncryptedStageAsync(
        IQueryable<Candidate> matching,
        string text,
        SearchOptions options,
        CancellationToken cancellationToken)
    {
        var protector = FieldEncryptionModel.ProtectorFrom(dbContext.GetService<IDbContextOptions>());
        var ids = matching.Select(candidate => candidate.Id);
        var stored = dbContext.CandidateCiphertexts.AsNoTracking().Where(row => ids.Contains(row.Id));
        var raw = text.Length > 0
            ? await stored
                .Select(row => new StoredRow(row.Id, row.FirstName, row.LastName, row.Email, row.Phone, row.Notes, row.Status, row.UpdatedAtUtc))
                .ToListAsync(cancellationToken)
            : await stored
                .Select(row => new StoredRow(row.Id, row.FirstName, row.LastName, null, null, null, row.Status, row.UpdatedAtUtc))
                .ToListAsync(cancellationToken);

        var term = EncryptedSearchSemantics.Fold(text);
        var byName = options.Sort.Field == SearchSortField.LastName;
        var decided = new EncryptedSearchRow?[raw.Count];
        Parallel.For(
            0,
            raw.Count,
            new ParallelOptions { CancellationToken = cancellationToken, MaxDegreeOfParallelism = Environment.ProcessorCount },
            index =>
            {
                var row = raw[index];
                string? first = null;
                string? last = null;
                string First() => first ??= protector.Unprotect(row.FirstName, FirstNameContext);
                string Last() => last ??= protector.Unprotect(row.LastName, LastNameContext);
                if (term.Length > 0
                    && !EncryptedSearchSemantics.Contains(First(), term)
                    && !EncryptedSearchSemantics.Contains(Last(), term)
                    && !EncryptedSearchSemantics.Contains(protector.Unprotect(row.Email!, EmailContext), term)
                    && !EncryptedSearchSemantics.Contains(protector.Unprotect(row.Phone!, PhoneContext), term)
                    && !EncryptedSearchSemantics.Contains(protector.Unprotect(row.Notes!, NotesContext), term))
                {
                    return;
                }
                decided[index] = new EncryptedSearchRow(
                    row.Id,
                    byName ? First() : string.Empty,
                    byName ? Last() : string.Empty,
                    row.Status,
                    row.UpdatedAtUtc);
            });
        var rows = new List<EncryptedSearchRow>(raw.Count);
        foreach (var row in decided)
        {
            if (row is not null)
            {
                rows.Add(row);
            }
        }

        rows.Sort(Comparer(options.Sort));
        var pageIds = rows
            .Skip((options.Page - 1) * options.PageSize)
            .Take(options.PageSize)
            .Select(row => row.Id)
            .ToList();
        var loaded = await Project(dbContext.Candidates.AsNoTracking().Where(candidate => pageIds.Contains(candidate.Id)))
            .ToListAsync(cancellationToken);
        var byId = loaded.ToDictionary(item => item.CandidateId);
        return new SearchPage<CandidateSearchItem>(
            [.. pageIds.Select(id => byId[id])],
            options.Page,
            options.PageSize,
            rows.Count);
    }

    private static readonly FieldContext FirstNameContext = FieldContext.For(CandidateConfiguration.Table, nameof(Candidate.FirstName));
    private static readonly FieldContext LastNameContext = FieldContext.For(CandidateConfiguration.Table, nameof(Candidate.LastName));
    private static readonly FieldContext EmailContext = FieldContext.For(CandidateConfiguration.Table, nameof(Candidate.Email));
    private static readonly FieldContext PhoneContext = FieldContext.For(CandidateConfiguration.Table, nameof(Candidate.Phone));
    private static readonly FieldContext NotesContext = FieldContext.For(CandidateConfiguration.Table, nameof(Candidate.Notes));

    /// <summary>Envelopes as stored; the unused fields are null when only the name order is needed.</summary>
    private sealed record StoredRow(
        Guid Id,
        string FirstName,
        string LastName,
        string? Email,
        string? Phone,
        string? Notes,
        string Status,
        DateTimeOffset UpdatedAtUtc);

    /// <summary>A row that matched, with the decrypted names only when the order needs them.</summary>
    private sealed record EncryptedSearchRow(
        Guid Id,
        string FirstName,
        string LastName,
        string Status,
        DateTimeOffset UpdatedAtUtc)
    {
        // Precomputed once per row: sorting compares identifiers many times.
        public string IdKey { get; } = Id.ToString("D");
    }

    /// <summary>
    /// The SQL ordering, reproduced: the chosen field in the chosen direction, then the
    /// identifier ascending in both directions.
    /// </summary>
    private static Comparison<EncryptedSearchRow> Comparer(SearchSort sort)
    {
        var sign = sort.Direction == SearchSortDirection.Ascending ? 1 : -1;
        Comparison<EncryptedSearchRow> field = sort.Field switch
        {
            SearchSortField.LastName => (left, right) =>
            {
                var byLast = EncryptedSearchSemantics.NameOrder.Compare(left.LastName, right.LastName);
                return byLast != 0 ? byLast : EncryptedSearchSemantics.NameOrder.Compare(left.FirstName, right.FirstName);
            },
            SearchSortField.Status => (left, right) => string.CompareOrdinal(left.Status, right.Status),
            _ => (left, right) => left.UpdatedAtUtc.UtcTicks.CompareTo(right.UpdatedAtUtc.UtcTicks),
        };
        return (left, right) =>
        {
            var byField = sign * field(left, right);
            return byField != 0 ? byField : string.CompareOrdinal(left.IdKey, right.IdKey);
        };
    }

    /// <summary>
    /// Orders, pages and projects the matching candidates into search items.
    /// </summary>
    /// <remarks>
    /// This lives beside the predicate rather than in the repository so the query-plan
    /// evidence can capture the statement the application actually issues — a plan for a
    /// reconstruction of the query would prove nothing about the query.
    /// </remarks>
    public IQueryable<CandidateSearchItem> Page(IQueryable<Candidate> matching, SearchOptions options) =>
        // The identifier ascending is always the final term, whichever field is chosen:
        // without it, two candidates sharing a sort value could appear on both of two
        // adjacent pages, or on neither.
        Project(Order(matching, options.Sort)
            .ThenBy(candidate => candidate.Id)
            .Skip((options.Page - 1) * options.PageSize)
            .Take(options.PageSize));

    private IQueryable<CandidateSearchItem> Project(IQueryable<Candidate> candidates) =>
        candidates
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
                candidate.UpdatedAtUtc,
                candidate.IsActive));

    /// <summary>
    /// Maps the validated sort onto a fixed ordering expression. The switch is over enum
    /// members only; no caller text reaches this point.
    /// </summary>
    /// <remarks>
    /// <c>status</c> orders by the status code, matching the previous browser ordering.
    /// <c>lastName</c> never reaches SQL: names are ciphertext, so that order is applied by
    /// <see cref="EncryptedStageAsync"/>.
    /// </remarks>
    private static IOrderedQueryable<Candidate> Order(IQueryable<Candidate> query, SearchSort sort)
    {
        var ascending = sort.Direction == SearchSortDirection.Ascending;
        return sort.Field switch
        {
            SearchSortField.LastName => throw new InvalidOperationException(
                "The last-name order is applied over decrypted values, never in SQL."),
            SearchSortField.Status => ascending
                ? query.OrderBy(candidate => candidate.Status)
                : query.OrderByDescending(candidate => candidate.Status),
            _ => ascending
                ? query.OrderBy(candidate => candidate.UpdatedAtUtc)
                : query.OrderByDescending(candidate => candidate.UpdatedAtUtc),
        };
    }

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

    private Expression<Func<Candidate, bool>> TagPredicate(ResolvedCriterion criterion) =>
        candidate => dbContext.CandidateTags.Any(relation =>
            relation.CandidateId == candidate.Id
            && criterion.ValueIds.Contains(relation.TagId));

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
    private sealed record CatalogMatch(Guid Id, string Family, string Name, int SortOrder);

    private async Task<IReadOnlyList<CatalogMatch>> ResolveCatalogAsync(
        SearchFiltersValue filters,
        CancellationToken cancellationToken)
    {
        var wanted = new List<(string Family, string Name)>();
        var levelFamilies = new HashSet<string>(StringComparer.Ordinal);
        Collect(filters.SkillCriteria, CatalogFamilies.Skill, CatalogFamilies.SkillLevel);
        Collect(filters.LanguageCriteria, CatalogFamilies.Language, CatalogFamilies.LanguageLevel);
        Collect(filters.ProgramCriteria, CatalogFamilies.Program, CatalogFamilies.ProgramLevel);
        Collect(filters.TagCriteria, CatalogFamilies.Tag, CatalogFamilies.Tag);

        void Collect(IReadOnlyList<SearchCriterion> criteria, string valueFamily, string levelFamily)
        {
            foreach (var criterion in criteria)
            {
                wanted.Add((valueFamily, criterion.NormalizedValue));
                if (!criterion.MatchesAnyLevel)
                {
                    wanted.Add((levelFamily, criterion.NormalizedLevel));
                    levelFamilies.Add(levelFamily);
                }
            }
        }

        if (wanted.Count == 0)
        {
            return [];
        }
        var families = wanted.Select(item => item.Family).Distinct().ToArray();
        var names = wanted.Select(item => item.Name).Distinct().ToArray();
        var rankedFamilies = levelFamilies.ToArray();
        var items = await dbContext.CatalogItems
            .AsNoTracking()
            .Where(item => (families.Contains(item.Family) && names.Contains(item.NameNormalized))
                || rankedFamilies.Contains(item.Family))
            .Select(item => new CatalogMatch(item.Id, item.Family, item.NameNormalized, item.SortOrder))
            .ToListAsync(cancellationToken);
        return items;
    }

    private static ResolvedCriterion Resolve(
        IReadOnlyList<CatalogMatch> catalog,
        SearchCriterion criterion,
        string valueFamily,
        string levelFamily)
    {
        var minimum = catalog.Where(item => item.Family == levelFamily
            && item.Name == criterion.NormalizedLevel).ToArray();
        return new ResolvedCriterion(
            [.. catalog.Where(item => item.Family == valueFamily
                && item.Name == criterion.NormalizedValue).Select(item => item.Id)],
            criterion.MatchesAnyLevel ? [] : [.. catalog.Where(item => item.Family == levelFamily
                && minimum.Any(level => item.SortOrder >= level.SortOrder)).Select(item => item.Id)],
            criterion.MatchesAnyLevel);
    }

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
