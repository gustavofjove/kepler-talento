using KeplerTalento.Application.Common.Errors;
using KeplerTalento.Domain.Candidates;
using KeplerTalento.Domain.Catalogs;

namespace KeplerTalento.Application.Features.Search;

/// <summary>
/// One filter line as a caller writes it: a catalog value and an optional level, where an
/// empty level means "any level for this value".
/// </summary>
public sealed record SearchCriterionInput(string? Value, string? Level);

/// <summary>
/// The complete filter value, exactly as the frontend's <c>SearchFilters</c> writes it.
/// Every member is nullable because this is the wire shape: an absent member is a filter
/// the caller did not set, which restricts nothing.
/// </summary>
public sealed record SearchFiltersInput(
    string? Text,
    IReadOnlyList<string?>? StatusValues,
    IReadOnlyList<SearchCriterionInput?>? SkillCriteria,
    string? SkillMode,
    IReadOnlyList<SearchCriterionInput?>? LanguageCriteria,
    string? LanguageMode,
    IReadOnlyList<SearchCriterionInput?>? ProgramCriteria,
    string? ProgramMode,
    string? HasCv);

public enum MultiValueMode
{
    Any,
    All,
}

public enum CvPresence
{
    /// <summary>No selection: primary-CV presence places no restriction.</summary>
    Unset,
    Present,
    Absent,
}

/// <summary>
/// A normalized criterion. <see cref="Value"/> and <see cref="Level"/> keep what the caller
/// wrote so a preset round-trips unchanged; the normalized pair is what the query compares
/// and what deduplication is decided on.
/// </summary>
public sealed record SearchCriterion(string Value, string NormalizedValue, string Level, string NormalizedLevel)
{
    /// <summary>An empty level matches any level for the same value.</summary>
    public bool MatchesAnyLevel => NormalizedLevel.Length == 0;
}

/// <summary>
/// The validated filter value. Reaching this type means every value is known, every blank
/// has been dropped and every duplicate criterion has been collapsed — so the query and the
/// preset store never have to re-decide any of it.
/// </summary>
public sealed record SearchFiltersValue(
    string Text,
    IReadOnlyList<string> StatusValues,
    IReadOnlyList<SearchCriterion> SkillCriteria,
    MultiValueMode SkillMode,
    IReadOnlyList<SearchCriterion> LanguageCriteria,
    MultiValueMode LanguageMode,
    IReadOnlyList<SearchCriterion> ProgramCriteria,
    MultiValueMode ProgramMode,
    CvPresence HasCv)
{
    /// <summary>
    /// True when the status family restricts nothing. Selecting every status and selecting
    /// none are the same query, and neither emits a predicate.
    /// </summary>
    public bool StatusIsUnrestricted =>
        StatusValues.Count == 0 || StatusValues.Count == CandidateStatuses.All.Count;

    public SearchFiltersInput ToInput() => new(
        Text,
        [.. StatusValues],
        [.. SkillCriteria.Select(ToInput)],
        ToWire(SkillMode),
        [.. LanguageCriteria.Select(ToInput)],
        ToWire(LanguageMode),
        [.. ProgramCriteria.Select(ToInput)],
        ToWire(ProgramMode),
        ToWire(HasCv));

    private static SearchCriterionInput ToInput(SearchCriterion criterion) =>
        new(criterion.Value, criterion.Level);

    public static string ToWire(MultiValueMode mode) => mode == MultiValueMode.All ? "ALL" : "ANY";

    public static string ToWire(CvPresence presence) => presence switch
    {
        CvPresence.Present => "yes",
        CvPresence.Absent => "no",
        _ => string.Empty,
    };
}

/// <summary>Stable error codes for the search and preset slices, with their Spanish messages.</summary>
public static class SearchErrors
{
    public const string StatusInvalid = "search.status.invalid";
    public const string ModeInvalid = "search.mode.invalid";
    public const string CvInvalid = "search.cv.invalid";
    public const string TextTooLong = "search.text.too_long";
    public const string CriterionTooLong = "search.criterion.too_long";
    public const string CriteriaTooMany = "search.criteria.too_many";
    public const string PageInvalid = "search.page.invalid";
    public const string PageSizeInvalid = "search.page_size.invalid";

    public const string PresetNotFound = "search_preset.not_found";
    public const string PresetNameRequired = "search_preset.name.required";
    public const string PresetNameTooLong = "search_preset.name.too_long";
    public const string PresetNameConflict = "search_preset.name.conflict";
    public const string PresetOwnerUnknown = "search_preset.owner.unknown";
    public const string PresetFiltersInvalid = "search_preset.filters.invalid";

    public const string StatusInvalidMessage = "El estado del candidato no es válido.";
    public const string ModeInvalidMessage = "El modo de combinación debe ser ANY o ALL.";
    public const string CvInvalidMessage = "El filtro de CV principal no es válido.";
    public const string TextTooLongMessage = "El texto de búsqueda es demasiado largo.";
    public const string CriterionTooLongMessage = "Un criterio de búsqueda es demasiado largo.";
    public const string CriteriaTooManyMessage = "Se han indicado demasiados criterios de búsqueda.";
    public const string PageInvalidMessage = "El número de página debe ser 1 o superior.";
    public const string PageSizeInvalidMessage = "El tamaño de página debe estar entre 1 y 100.";

    public const string PresetNotFoundMessage = "Búsqueda guardada no encontrada.";
    public const string PresetNameRequiredMessage = "El nombre de la búsqueda guardada es obligatorio.";
    public const string PresetNameTooLongMessage = "El nombre de la búsqueda guardada es demasiado largo.";
    public const string PresetNameConflictMessage = "Ya tiene una búsqueda guardada con ese nombre.";
    public const string PresetOwnerUnknownMessage = "No se ha podido determinar el propietario de la búsqueda guardada.";
    public const string PresetFiltersInvalidMessage = "Los filtros de la búsqueda guardada no son válidos.";
}

/// <summary>
/// Turns the wire filter value into the validated one, or into the caller's refusal.
/// </summary>
/// <remarks>
/// Search and presets share this deliberately. A preset that stored a filter the search
/// endpoint would refuse is a preset that cannot be applied, so exactly one definition of
/// "a valid filter value" exists and both writes go through it.
///
/// Everything here is a pure function of the request. It performs no catalog lookup: a
/// criterion naming a value no candidate holds is a search that matches nothing, not a
/// failed request, and refusing it would leak which catalog values exist to a caller who
/// may not read the catalog.
/// </remarks>
public static class SearchFilterNormalization
{
    /// <summary>
    /// Version of the stored filter document. Presets keep it alongside their filters so a
    /// future shape change can be an explicit upgrade rather than a guess about what an old
    /// row meant.
    /// </summary>
    public const int FilterSchemaVersion = 1;

    public const int MaximumTextLength = 200;
    public const int MaximumCriterionLength = 200;

    /// <summary>
    /// A ceiling on criteria per family. Each <c>ALL</c> criterion becomes its own
    /// correlated existence condition, so an unbounded list is an unbounded query.
    /// </summary>
    public const int MaximumCriteriaPerFamily = 25;

    public static SearchFiltersValue Normalize(SearchFiltersInput? input, string property)
    {
        var issues = new List<ValidationIssue>();
        var value = TryNormalize(input, property, issues);
        return issues.Count > 0 ? throw new RequestValidationException(issues) : value;
    }

    /// <summary>
    /// Normalizes and collects issues rather than throwing, so one refusal can report every
    /// problem in the request instead of only the first.
    /// </summary>
    public static SearchFiltersValue TryNormalize(
        SearchFiltersInput? input,
        string property,
        List<ValidationIssue> issues)
    {
        input ??= new SearchFiltersInput(null, null, null, null, null, null, null, null, null);

        var text = (input.Text ?? string.Empty).Trim();
        if (text.Length > MaximumTextLength)
        {
            issues.Add(new ValidationIssue(
                $"{property}.Text",
                SearchErrors.TextTooLong,
                SearchErrors.TextTooLongMessage));
            text = string.Empty;
        }

        return new SearchFiltersValue(
            text,
            NormalizeStatuses(input.StatusValues, $"{property}.StatusValues", issues),
            NormalizeCriteria(input.SkillCriteria, $"{property}.SkillCriteria", issues),
            NormalizeMode(input.SkillMode, $"{property}.SkillMode", issues),
            NormalizeCriteria(input.LanguageCriteria, $"{property}.LanguageCriteria", issues),
            NormalizeMode(input.LanguageMode, $"{property}.LanguageMode", issues),
            NormalizeCriteria(input.ProgramCriteria, $"{property}.ProgramCriteria", issues),
            NormalizeMode(input.ProgramMode, $"{property}.ProgramMode", issues),
            NormalizeCv(input.HasCv, $"{property}.HasCv", issues));
    }

    private static IReadOnlyList<string> NormalizeStatuses(
        IReadOnlyList<string?>? statuses,
        string property,
        List<ValidationIssue> issues)
    {
        if (statuses is null || statuses.Count == 0)
        {
            // No selection and every status selected are the same query. Answering with the
            // complete set keeps a stored preset readable rather than ambiguous.
            return [.. CandidateStatuses.All];
        }
        var accepted = new List<string>();
        var invalid = false;
        foreach (var status in statuses)
        {
            var trimmed = (status ?? string.Empty).Trim();
            if (trimmed.Length == 0)
            {
                continue;
            }
            if (!CandidateStatuses.IsKnown(trimmed))
            {
                invalid = true;
                continue;
            }
            if (!accepted.Contains(trimmed, StringComparer.Ordinal))
            {
                accepted.Add(trimmed);
            }
        }
        if (invalid)
        {
            issues.Add(new ValidationIssue(property, SearchErrors.StatusInvalid, SearchErrors.StatusInvalidMessage));
        }
        // An entirely blank selection restricts nothing, exactly as an absent one does.
        return accepted.Count == 0 ? [.. CandidateStatuses.All] : accepted;
    }

    private static IReadOnlyList<SearchCriterion> NormalizeCriteria(
        IReadOnlyList<SearchCriterionInput?>? criteria,
        string property,
        List<ValidationIssue> issues)
    {
        if (criteria is null || criteria.Count == 0)
        {
            return [];
        }
        if (criteria.Count > MaximumCriteriaPerFamily)
        {
            issues.Add(new ValidationIssue(
                property,
                SearchErrors.CriteriaTooMany,
                SearchErrors.CriteriaTooManyMessage));
            return [];
        }
        var accepted = new List<SearchCriterion>();
        var tooLong = false;
        foreach (var criterion in criteria)
        {
            var value = (criterion?.Value ?? string.Empty).Trim();
            var level = (criterion?.Level ?? string.Empty).Trim();
            // A blank value names nothing, so it is dropped rather than refused: the filter
            // panel produces one every time a user adds a line before choosing its value.
            if (value.Length == 0)
            {
                continue;
            }
            if (value.Length > MaximumCriterionLength || level.Length > MaximumCriterionLength)
            {
                tooLong = true;
                continue;
            }
            var normalized = new SearchCriterion(
                value,
                CatalogName.Normalize(value),
                level,
                CatalogName.Normalize(level));
            // Repeating a criterion cannot change ANY, and under ALL it would otherwise add
            // a redundant existence condition for a requirement already stated.
            if (!accepted.Any(existing =>
                string.Equals(existing.NormalizedValue, normalized.NormalizedValue, StringComparison.Ordinal)
                && string.Equals(existing.NormalizedLevel, normalized.NormalizedLevel, StringComparison.Ordinal)))
            {
                accepted.Add(normalized);
            }
        }
        if (tooLong)
        {
            issues.Add(new ValidationIssue(
                property,
                SearchErrors.CriterionTooLong,
                SearchErrors.CriterionTooLongMessage));
        }
        return accepted;
    }

    private static MultiValueMode NormalizeMode(string? mode, string property, List<ValidationIssue> issues)
    {
        var trimmed = (mode ?? string.Empty).Trim();
        if (trimmed.Length == 0 || trimmed.Equals("ANY", StringComparison.OrdinalIgnoreCase))
        {
            return MultiValueMode.Any;
        }
        if (trimmed.Equals("ALL", StringComparison.OrdinalIgnoreCase))
        {
            return MultiValueMode.All;
        }
        issues.Add(new ValidationIssue(property, SearchErrors.ModeInvalid, SearchErrors.ModeInvalidMessage));
        return MultiValueMode.Any;
    }

    private static CvPresence NormalizeCv(string? hasCv, string property, List<ValidationIssue> issues)
    {
        var trimmed = (hasCv ?? string.Empty).Trim();
        if (trimmed.Length == 0)
        {
            return CvPresence.Unset;
        }
        if (trimmed.Equals("yes", StringComparison.OrdinalIgnoreCase))
        {
            return CvPresence.Present;
        }
        if (trimmed.Equals("no", StringComparison.OrdinalIgnoreCase))
        {
            return CvPresence.Absent;
        }
        issues.Add(new ValidationIssue(property, SearchErrors.CvInvalid, SearchErrors.CvInvalidMessage));
        return CvPresence.Unset;
    }
}

/// <summary>
/// Turns free text into an <c>ILIKE</c> pattern that means what the user typed.
/// </summary>
/// <remarks>
/// A name containing <c>%</c> or <c>_</c> must match those characters, not act as SQL
/// wildcard syntax. The escape character itself is escaped first, otherwise a trailing
/// backslash would escape the closing wildcard the pattern appends.
/// </remarks>
public static class SearchTextPattern
{
    public const char EscapeCharacter = '\\';

    public static string Escape(string text) => text
        .Replace("\\", "\\\\", StringComparison.Ordinal)
        .Replace("%", "\\%", StringComparison.Ordinal)
        .Replace("_", "\\_", StringComparison.Ordinal);

    /// <summary>The case-insensitive substring pattern for one search term.</summary>
    public static string Contains(string text) => $"%{Escape(text)}%";
}
