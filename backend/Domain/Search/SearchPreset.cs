using KeplerTalento.Domain.Catalogs;

namespace KeplerTalento.Domain.Search;

/// <summary>
/// Uniqueness comparison of saved-search names: whitespace, casing and accents are ignored.
/// </summary>
/// <remarks>
/// KTL-14 made presets a shared library, so a preset name is now a shared vocabulary in the
/// same sense as a catalog name: "Ingles B2" and "Inglés B2" would be two entries every
/// recruiter has to tell apart, which is a mistake to prevent rather than a choice to allow.
/// The comparison is therefore the catalog one, not a copy of it.
/// </remarks>
public static class SearchPresetName
{
    public const int MaximumLength = 120;

    public static string Normalize(string? name) => CatalogName.Normalize(name);

    public static bool IsValid(string? name)
    {
        var trimmed = (name ?? string.Empty).Trim();
        return trimmed.Length is > 0 and <= MaximumLength;
    }
}

/// <summary>
/// A saved search in the shared library. Administrators curate it; everyone who can search
/// candidates applies it.
/// </summary>
/// <remarks>
/// The filter value is held as a JSON document rather than as child rows. Nothing queries
/// across presets by filter content — they are read whole — so normalizing them into a dozen
/// tables would add write complexity that buys no query. The document carries its own schema
/// version so a future shape change can be an explicit upgrade instead of a guess about what
/// an old row meant.
///
/// There is deliberately no owner or author. Nothing in the product shows who wrote a preset,
/// and an actor key stored for no purpose is personal data held for no purpose.
///
/// The preset name and its filters can still contain personal data, so they are stored,
/// never logged.
/// </remarks>
public sealed class SearchPreset
{
    private SearchPreset() { }

    public SearchPreset(
        Guid id,
        string name,
        string filtersJson,
        int filterSchemaVersion,
        DateTimeOffset createdAtUtc)
    {
        Id = id;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = createdAtUtc;
        Version = 1;
        ApplyName(name);
        ApplyFilters(filtersJson, filterSchemaVersion);
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; } = string.Empty;

    /// <summary>The folded uniqueness key; carries the library-wide unique index.</summary>
    public string NormalizedName { get; private set; } = string.Empty;

    /// <summary>The complete filter value as a JSON object.</summary>
    public string Filters { get; private set; } = "{}";

    public int FilterSchemaVersion { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    /// <summary>When the preset's content last changed. Applying it is not a change.</summary>
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    /// <summary>When someone last applied this preset; null until anyone has.</summary>
    public DateTimeOffset? LastUsedAtUtc { get; private set; }

    /// <summary>
    /// The optimistic-concurrency version of the preset's content.
    /// </summary>
    /// <remarks>
    /// An ordinary counter rather than PostgreSQL's <c>xmin</c>, which the other aggregates use.
    /// <c>xmin</c> moves on every row update, and applying a preset writes its last-used time:
    /// with <c>xmin</c> every recruiter applying a preset would invalidate the version an
    /// administrator is editing against. Only <see cref="Update"/> advances this.
    /// </remarks>
    public int Version { get; private set; }

    /// <summary>Renames the preset and replaces its filters as one change, one version.</summary>
    public void Update(string name, string filtersJson, int filterSchemaVersion, DateTimeOffset updatedAtUtc)
    {
        ApplyName(name);
        ApplyFilters(filtersJson, filterSchemaVersion);
        UpdatedAtUtc = updatedAtUtc;
        Version++;
    }

    /// <summary>
    /// Records an apply. It touches neither the update time nor the version: using a preset
    /// does not change what it is, and must not make an edit in progress stale.
    /// </summary>
    public void MarkUsed(DateTimeOffset usedAtUtc) => LastUsedAtUtc = usedAtUtc;

    private void ApplyName(string name)
    {
        if (!SearchPresetName.IsValid(name))
        {
            throw new ArgumentException("A saved search needs a non-blank, bounded name.", nameof(name));
        }
        Name = name.Trim();
        NormalizedName = SearchPresetName.Normalize(Name);
    }

    private void ApplyFilters(string filtersJson, int filterSchemaVersion)
    {
        if (string.IsNullOrWhiteSpace(filtersJson))
        {
            throw new ArgumentException("A saved search always stores a filter value.", nameof(filtersJson));
        }
        Filters = filtersJson;
        FilterSchemaVersion = filterSchemaVersion;
    }
}
