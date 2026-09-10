namespace KeplerTalento.Domain.Search;

/// <summary>
/// Case-insensitive comparison of saved-search names, matching what the browser-side service
/// did before presets moved to the API.
/// </summary>
/// <remarks>
/// This deliberately does not fold accents, unlike <c>CatalogName.Normalize</c>. A catalog
/// name is a shared vocabulary where <c>"Ingles"</c> and <c>"Inglés"</c> must be the same
/// entry; a saved-search name is one person's private label, and refusing "Búsqueda" because
/// they already have "Busqueda" would be a surprise rather than a safeguard.
/// </remarks>
public static class SearchPresetName
{
    public const int MaximumLength = 120;

    public static string Normalize(string? name) => (name ?? string.Empty).Trim().ToLowerInvariant();

    public static bool IsValid(string? name)
    {
        var trimmed = (name ?? string.Empty).Trim();
        return trimmed.Length is > 0 and <= MaximumLength;
    }
}

/// <summary>
/// One person's saved search. Private to its owner: presets are never shared, and the owner
/// is derived from the current actor rather than supplied by the caller.
/// </summary>
/// <remarks>
/// The filter value is held as a JSON document rather than as child rows. Nothing queries
/// across presets by filter content — they are read whole, by their owner — so normalizing
/// them into a dozen tables would add write complexity that buys no query. The document
/// carries its own schema version so a future shape change can be an explicit upgrade
/// instead of a guess about what an old row meant.
///
/// The preset name and its filters can contain personal data: an owner is free to save
/// "Candidatos de Marta". They are therefore stored, never logged.
/// </remarks>
public sealed class SearchPreset
{
    private SearchPreset() { }

    public SearchPreset(
        Guid id,
        string ownerId,
        string name,
        string filtersJson,
        int filterSchemaVersion,
        DateTimeOffset createdAtUtc)
    {
        if (string.IsNullOrWhiteSpace(ownerId))
        {
            throw new ArgumentException("A saved search always belongs to a known owner.", nameof(ownerId));
        }
        Id = id;
        OwnerId = ownerId.Trim();
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = createdAtUtc;
        FilterSchemaVersion = filterSchemaVersion;
        Rename(name, createdAtUtc);
        ReplaceFilters(filtersJson, filterSchemaVersion, createdAtUtc);
    }

    public Guid Id { get; private set; }

    /// <summary>
    /// The current actor's stable key. There is deliberately no foreign key: the identity
    /// schema does not exist yet, and inventing a parallel user table here would couple
    /// saved searches to an identity design nobody has approved.
    /// </summary>
    public string OwnerId { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    /// <summary>The case-folded uniqueness key; carries the per-owner unique index.</summary>
    public string NormalizedName { get; private set; } = string.Empty;

    /// <summary>The complete filter value as a JSON object.</summary>
    public string Filters { get; private set; } = "{}";

    public int FilterSchemaVersion { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    /// <summary>When the owner last applied this preset; null until they have.</summary>
    public DateTimeOffset? LastUsedAtUtc { get; private set; }

    public void Rename(string name, DateTimeOffset updatedAtUtc)
    {
        if (!SearchPresetName.IsValid(name))
        {
            throw new ArgumentException("A saved search needs a non-blank, bounded name.", nameof(name));
        }
        Name = name.Trim();
        NormalizedName = SearchPresetName.Normalize(Name);
        UpdatedAtUtc = updatedAtUtc;
    }

    public void ReplaceFilters(string filtersJson, int filterSchemaVersion, DateTimeOffset updatedAtUtc)
    {
        if (string.IsNullOrWhiteSpace(filtersJson))
        {
            throw new ArgumentException("A saved search always stores a filter value.", nameof(filtersJson));
        }
        Filters = filtersJson;
        FilterSchemaVersion = filterSchemaVersion;
        UpdatedAtUtc = updatedAtUtc;
    }

    /// <summary>
    /// Records an apply. Both timestamps move together and in one write, so "last used" can
    /// never be newer than the row it describes.
    /// </summary>
    public void MarkUsed(DateTimeOffset usedAtUtc)
    {
        LastUsedAtUtc = usedAtUtc;
        UpdatedAtUtc = usedAtUtc;
    }
}
