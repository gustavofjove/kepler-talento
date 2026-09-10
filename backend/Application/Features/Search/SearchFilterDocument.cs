using System.Text.Json;
using System.Text.Json.Serialization;
using KeplerTalento.Application.Common.Errors;

namespace KeplerTalento.Application.Features.Search;

/// <summary>
/// How a saved search's filter value is written to and read back from its JSONB column.
/// </summary>
/// <remarks>
/// The stored document carries its own <c>version</c> alongside the filter members, which
/// are the same names the frontend uses. Two things follow: a stored row is readable without
/// the code that wrote it, and a future shape change can be an explicit upgrade rather than
/// an inference about what an old row meant.
///
/// Reading re-normalizes and re-validates. The database's JSON check can only say "this is
/// an object"; whether it is a filter value this application understands is a question only
/// the application can answer, and a row that fails it is refused rather than half-applied.
/// </remarks>
public static class SearchFilterDocument
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    private sealed record StoredCriterion(string? Value, string? Level);

    private sealed record StoredDocument(
        int Version,
        string? Text,
        IReadOnlyList<string?>? StatusValues,
        IReadOnlyList<StoredCriterion?>? SkillCriteria,
        string? SkillMode,
        IReadOnlyList<StoredCriterion?>? LanguageCriteria,
        string? LanguageMode,
        IReadOnlyList<StoredCriterion?>? ProgramCriteria,
        string? ProgramMode,
        string? HasCv);

    public static string Serialize(SearchFiltersValue filters)
    {
        var input = filters.ToInput();
        return JsonSerializer.Serialize(
            new StoredDocument(
                SearchFilterNormalization.FilterSchemaVersion,
                input.Text,
                input.StatusValues,
                [.. (input.SkillCriteria ?? []).Select(ToStored)],
                input.SkillMode,
                [.. (input.LanguageCriteria ?? []).Select(ToStored)],
                input.LanguageMode,
                [.. (input.ProgramCriteria ?? []).Select(ToStored)],
                input.ProgramMode,
                input.HasCv),
            Options);
    }

    /// <summary>
    /// Reads a stored filter value back, or refuses it. A row that cannot be understood is
    /// never silently replaced by empty filters: that would quietly turn a saved search into
    /// "every candidate", which is the opposite of what its owner asked for.
    /// </summary>
    public static SearchFiltersValue Parse(string json)
    {
        StoredDocument? stored;
        try
        {
            stored = JsonSerializer.Deserialize<StoredDocument>(json, Options);
        }
        catch (JsonException)
        {
            stored = null;
        }
        if (stored is null || stored.Version != SearchFilterNormalization.FilterSchemaVersion)
        {
            throw Invalid();
        }
        var issues = new List<ValidationIssue>();
        var value = SearchFilterNormalization.TryNormalize(
            new SearchFiltersInput(
                stored.Text,
                stored.StatusValues,
                [.. (stored.SkillCriteria ?? []).Select(ToInput)],
                stored.SkillMode,
                [.. (stored.LanguageCriteria ?? []).Select(ToInput)],
                stored.LanguageMode,
                [.. (stored.ProgramCriteria ?? []).Select(ToInput)],
                stored.ProgramMode,
                stored.HasCv),
            "Filters",
            issues);
        return issues.Count > 0 ? throw Invalid() : value;
    }

    private static StoredCriterion? ToStored(SearchCriterionInput? criterion) =>
        criterion is null ? null : new StoredCriterion(criterion.Value, criterion.Level);

    private static SearchCriterionInput? ToInput(StoredCriterion? criterion) =>
        criterion is null ? null : new SearchCriterionInput(criterion.Value, criterion.Level);

    private static RequestValidationException Invalid() =>
        new([new ValidationIssue(
            "Filters",
            SearchErrors.PresetFiltersInvalid,
            SearchErrors.PresetFiltersInvalidMessage)]);
}
