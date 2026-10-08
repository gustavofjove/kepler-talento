namespace KeplerTalento.Domain.Catalogs;

/// <summary>
/// The closed palette a catalog value's chip is drawn in (KTL-41). Values are stable tokens,
/// never colour codes: the hues live in the SPA's stylesheet. The order is the order the SPA
/// offers them in, and must match <c>CATALOG_COLORS</c> in the frontend.
/// </summary>
public static class CatalogColors
{
    public const string Orange = "orange";
    public const string Yellow = "yellow";
    public const string Green = "green";
    public const string Teal = "teal";
    public const string Blue = "blue";
    public const string Indigo = "indigo";
    public const string Violet = "violet";
    public const string Pink = "pink";
    public const string Grey = "grey";

    /// <summary>The colour every value holds unless an administrator picks another one.</summary>
    public const string Default = Orange;

    public static readonly IReadOnlyList<string> All =
    [
        Orange,
        Yellow,
        Green,
        Teal,
        Blue,
        Indigo,
        Violet,
        Pink,
        Grey,
    ];

    /// <summary>The families shown as chips, the only ones whose values may leave the default.</summary>
    private static readonly IReadOnlyList<string> ColorableFamilies =
    [
        CatalogFamilies.Skill,
        CatalogFamilies.Language,
        CatalogFamilies.Program,
        CatalogFamilies.Tag,
    ];

    public static bool IsKnown(string? color) =>
        color is not null && All.Contains(color, StringComparer.Ordinal);

    public static bool Supports(string? family) =>
        family is not null && ColorableFamilies.Contains(family, StringComparer.Ordinal);
}
