using KeplerTalento.Domain.Search;

namespace KeplerTalento.Application.Abstractions.Persistence;

public enum SearchPresetSaveOutcome
{
    Saved,

    /// <summary>
    /// The library-wide unique name index refused the write. This is the authoritative answer
    /// under concurrency: two requests can both find the name free and only one can store it.
    /// </summary>
    NameConflict,

    /// <summary>The preset changed or disappeared after the caller read the version it sent.</summary>
    ConcurrencyConflict,
}

/// <summary>Persistence for the shared saved-search library.</summary>
public interface ISearchPresetRepository
{
    /// <summary>Ordered by normalized name, so listing is case- and accent-insensitively alphabetical.</summary>
    Task<IReadOnlyList<SearchPreset>> ListAsync(CancellationToken cancellationToken);

    Task<SearchPreset?> FindAsync(Guid id, CancellationToken cancellationToken);

    void Add(SearchPreset preset);

    void Remove(SearchPreset preset);

    /// <summary>
    /// Declares the version the caller read, so an update or delete against a stale version is
    /// refused by the database rather than silently overwriting someone else's change.
    /// </summary>
    void ExpectVersion(SearchPreset preset, uint version);

    Task<SearchPresetSaveOutcome> SaveAsync(CancellationToken cancellationToken);
}
