using KeplerTalento.Domain.Catalogs;

namespace KeplerTalento.Application.Abstractions.Persistence;

public enum CatalogSaveOutcome
{
    Saved,
    DuplicateName,
    DuplicateCode,
    ConcurrencyConflict,
}

public interface ICatalogRepository
{
    /// <summary>Returns a family ordered by position, optionally including inactive values.</summary>
    Task<IReadOnlyList<CatalogItem>> ListAsync(string family, bool includeInactive, CancellationToken cancellationToken);

    Task<CatalogItem?> FindAsync(string family, Guid id, CancellationToken cancellationToken);

    void Add(CatalogItem item);

    /// <summary>
    /// Declares the version the caller read, so a write against a stale version is rejected
    /// rather than silently overwriting a concurrent change.
    /// </summary>
    void ExpectVersion(CatalogItem item, uint version);

    /// <summary>
    /// Persists the pending changes together with one audit event, in a single transaction,
    /// so a rejected change can never leave an applied-change event behind.
    /// </summary>
    Task<CatalogSaveOutcome> SaveAsync(string auditEventType, string subjectId, CancellationToken cancellationToken);

    /// <summary>
    /// Whether a catalog value is referenced by a candidate relation. Candidate relations
    /// move server-side in KTL-8; until then this reports false and the rule is carried by
    /// the transitional frontend pre-check.
    /// </summary>
    Task<bool> IsValueInUseAsync(string family, string nameNormalized, CancellationToken cancellationToken);
}
