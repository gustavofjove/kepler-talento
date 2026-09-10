using KeplerTalento.Domain.Search;

namespace KeplerTalento.Application.Abstractions.Persistence;

public enum SearchPresetSaveOutcome
{
    Saved,

    /// <summary>
    /// The per-owner unique index refused the write. This is the authoritative answer under
    /// concurrency: two requests can both find the name free and only one can store it.
    /// </summary>
    NameConflict,
}

/// <summary>
/// Persistence for one person's saved searches.
/// </summary>
/// <remarks>
/// Every member takes the owner, and every predicate includes it. That is not defensive
/// duplication of the handler's check: it is what makes "a preset identifier from another
/// owner is indistinguishable from one that does not exist" a property of the query rather
/// than of whoever remembered to add the filter.
/// </remarks>
public interface ISearchPresetRepository
{
    /// <summary>Ordered by normalized name, so listing is case-insensitively alphabetical.</summary>
    Task<IReadOnlyList<SearchPreset>> ListAsync(string ownerId, CancellationToken cancellationToken);

    Task<SearchPreset?> FindAsync(string ownerId, Guid id, CancellationToken cancellationToken);

    void Add(SearchPreset preset);

    void Remove(SearchPreset preset);

    Task<SearchPresetSaveOutcome> SaveAsync(CancellationToken cancellationToken);
}
