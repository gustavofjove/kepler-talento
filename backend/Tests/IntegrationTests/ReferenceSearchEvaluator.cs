using KeplerTalento.Application.Features.Search;

namespace KeplerTalento.Tests.IntegrationTests;

/// <summary>
/// The pre-KTL-10 search semantics, preserved as a second opinion.
/// </summary>
/// <remarks>
/// This is a deliberate transcription of the browser-side <c>CandidateSearchService</c> that
/// KTL-10 replaces: the same field list for free text, the same
/// <c>trim().toLocaleLowerCase()</c> comparison, the same <c>ANY</c>/<c>ALL</c> rules and the
/// same treatment of an empty criterion level. It exists so "the new query returns what the
/// old one did" is an assertion against an independent implementation rather than against
/// expectations hand-written from the same understanding that produced the SQL.
///
/// It is test-only and intentionally naive — it filters a list in memory, which is precisely
/// the thing KTL-10 stops doing in production.
/// </remarks>
public static class ReferenceSearchEvaluator
{
    /// <summary>
    /// Matching candidate identifiers in the order the server promises:
    /// <c>UpdatedAtUtc</c> descending, identifier ascending as the tie-breaker.
    /// </summary>
    public static IReadOnlyList<Guid> Evaluate(
        IEnumerable<ParityCandidate> candidates,
        SearchFiltersInput filters)
    {
        var text = (filters.Text ?? string.Empty).Trim().ToLowerInvariant();
        var statuses = (filters.StatusValues ?? [])
            .Where(status => !string.IsNullOrWhiteSpace(status))
            .Select(status => status!.Trim())
            .ToList();
        var hasCv = (filters.HasCv ?? string.Empty).Trim().ToLowerInvariant();

        return
        [
            .. candidates
                .Where(candidate => candidate.IsActive)
                .Where(candidate => text.Length == 0 || string.Join(
                    ' ',
                    candidate.FirstName,
                    candidate.LastName,
                    candidate.Email,
                    candidate.Phone,
                    candidate.Notes).ToLowerInvariant().Contains(text, StringComparison.Ordinal))
                .Where(candidate => statuses.Count == 0
                    || statuses.Contains(candidate.Status, StringComparer.Ordinal))
                .Where(candidate => hasCv.Length == 0
                    || (hasCv == "yes") == (candidate.PrimaryDocumentId is not null))
                .Where(candidate => Matches(candidate.Skills, filters.SkillCriteria, filters.SkillMode))
                .Where(candidate => Matches(candidate.Languages, filters.LanguageCriteria, filters.LanguageMode))
                .Where(candidate => Matches(candidate.Programs, filters.ProgramCriteria, filters.ProgramMode))
                .OrderByDescending(candidate => candidate.UpdatedAtUtc)
                .ThenBy(candidate => candidate.Id)
                .Select(candidate => candidate.Id),
        ];
    }

    private static bool Matches(
        IReadOnlyList<ParityRelation> held,
        IReadOnlyList<SearchCriterionInput?>? criteria,
        string? mode)
    {
        var wanted = (criteria ?? [])
            .Where(criterion => !string.IsNullOrWhiteSpace(criterion?.Value))
            .ToList();
        if (wanted.Count == 0)
        {
            return true;
        }
        bool MatchesOne(SearchCriterionInput? criterion) => held.Any(relation =>
            Equals(relation.Value, criterion!.Value)
            && (string.IsNullOrWhiteSpace(criterion.Level) || Equals(relation.Level, criterion.Level)));

        return string.Equals(mode, "ALL", StringComparison.OrdinalIgnoreCase)
            ? wanted.All(MatchesOne)
            : wanted.Any(MatchesOne);
    }

    private static bool Equals(string? left, string? right) => string.Equals(
        (left ?? string.Empty).Trim(),
        (right ?? string.Empty).Trim(),
        StringComparison.OrdinalIgnoreCase);
}
