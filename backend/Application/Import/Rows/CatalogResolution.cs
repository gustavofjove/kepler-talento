namespace KeplerTalento.Application.Import.Rows;

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
