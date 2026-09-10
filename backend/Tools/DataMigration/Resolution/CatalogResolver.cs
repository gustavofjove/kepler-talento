using KeplerTalento.Domain.Catalogs;

namespace KeplerTalento.Tools.DataMigration.Resolution;

/// <summary>
/// The outcome of resolving one free-text source value against the catalog.
/// </summary>
public readonly record struct CatalogResolution(Guid? CatalogItemId, ResolutionStep Step)
{
    public bool Resolved => CatalogItemId is not null;

    public static CatalogResolution Unresolved => new(null, ResolutionStep.None);
}

public enum ResolutionStep
{
    None,
    ExactName,
    NormalizedCode,
    OperatorMapping,
}

/// <summary>
/// One free-text value that resolved to nothing, with how often it occurred. The value is
/// catalog vocabulary — a language, a sector, a skill — not personal data, and it is
/// reported precisely so an operator can decide what it means.
/// </summary>
public sealed record UnresolvedValue(string Family, string Value, int Occurrences);

/// <summary>
/// Resolves the free text Access holds onto existing catalog entries.
/// </summary>
/// <remarks>
/// Three ordered steps, and no fourth: exact name, then the same normalization the catalog
/// itself uses for uniqueness, then an explicit operator mapping. There is deliberately no
/// fuzzy or similarity matching — a near-match that silently picks the wrong skill is worse
/// than a rejection an operator resolves once in a mapping file. The resolver never creates
/// a catalog entry; new vocabulary is added through the catalog administration screens.
/// </remarks>
public sealed class CatalogResolver
{
    private readonly Dictionary<(string Family, string Name), Guid> _byExactName = new();
    private readonly Dictionary<(string Family, string Code), Guid> _byCode = new();
    private readonly Dictionary<(string Family, string Value), string> _operatorMappings;
    private readonly Dictionary<(string Family, string Value), int> _unresolved = new();
    private readonly Dictionary<(string Family, string Value), int> _broken = new();

    public CatalogResolver(
        IEnumerable<CatalogEntry> catalog,
        IReadOnlyDictionary<(string Family, string Value), string>? operatorMappings = null)
    {
        foreach (var entry in catalog)
        {
            _byExactName[(entry.Family, entry.Name)] = entry.Id;
            _byCode[(entry.Family, entry.Code)] = entry.Id;
        }
        _operatorMappings = operatorMappings is null
            ? new Dictionary<(string, string), string>()
            : new Dictionary<(string, string), string>(operatorMappings);
    }

    public sealed record CatalogEntry(Guid Id, string Family, string Code, string Name);

    /// <summary>
    /// Values the operator mapped to a catalog code that does not exist. A mapping pointing
    /// nowhere is an operator error and must be reported rather than treated as unresolved,
    /// or they would fix the same value twice.
    /// </summary>
    public IReadOnlyList<UnresolvedValue> BrokenMappings => Summarize(_broken);

    public CatalogResolution Resolve(string family, string? value)
    {
        var trimmed = (value ?? string.Empty).Trim();
        if (trimmed.Length == 0)
        {
            return CatalogResolution.Unresolved;
        }

        if (_byExactName.TryGetValue((family, trimmed), out var exact))
        {
            return new CatalogResolution(exact, ResolutionStep.ExactName);
        }

        // The same normalization the catalog uses for its own uniqueness, so this step
        // introduces no matching semantics the product does not already have.
        var derived = CatalogName.DeriveCode(trimmed);
        if (_byCode.TryGetValue((family, derived), out var byCode))
        {
            return new CatalogResolution(byCode, ResolutionStep.NormalizedCode);
        }

        if (_operatorMappings.TryGetValue((family, trimmed), out var mappedCode))
        {
            if (_byCode.TryGetValue((family, mappedCode), out var mapped))
            {
                return new CatalogResolution(mapped, ResolutionStep.OperatorMapping);
            }
            // The mapping names a code the catalog does not have. Record it distinctly.
            Record(family, trimmed, broken: true);
            return CatalogResolution.Unresolved;
        }

        Record(family, trimmed, broken: false);
        return CatalogResolution.Unresolved;
    }

    private void Record(string family, string value, bool broken)
    {
        var target = broken ? _broken : _unresolved;
        target[(family, value)] = target.GetValueOrDefault((family, value)) + 1;
    }

    /// <summary>
    /// Every value that resolved to nothing, with occurrence counts, ordered so the report
    /// is stable between runs.
    /// </summary>
    public IReadOnlyList<UnresolvedValue> UnresolvedValues => Summarize(_unresolved);

    private static IReadOnlyList<UnresolvedValue> Summarize(
        Dictionary<(string Family, string Value), int> counts) =>
        [.. counts
            .Select(pair => new UnresolvedValue(pair.Key.Family, pair.Key.Value, pair.Value))
            .OrderBy(entry => entry.Family, StringComparer.Ordinal)
            .ThenBy(entry => entry.Value, StringComparer.Ordinal)];
}
