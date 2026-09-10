using KeplerTalento.Application.Abstractions.Persistence;
using KeplerTalento.Domain.Search;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace KeplerTalento.Infrastructure.Persistence;

/// <summary>
/// Saved-search persistence. Every query is owner-scoped in the predicate itself, so no
/// caller can reach another owner's row by knowing its identifier.
/// </summary>
public sealed class SearchPresetRepository(ApplicationDbContext dbContext) : ISearchPresetRepository
{
    public async Task<IReadOnlyList<SearchPreset>> ListAsync(
        string ownerId,
        CancellationToken cancellationToken) =>
        await dbContext.SearchPresets
            .AsNoTracking()
            .Where(preset => preset.OwnerId == ownerId)
            // Ordering on the normalized name is what makes the listing case-insensitively
            // alphabetical, and it is the column the owner index is sorted by, so the order
            // costs nothing.
            .OrderBy(preset => preset.NormalizedName)
            .ToListAsync(cancellationToken);

    public Task<SearchPreset?> FindAsync(string ownerId, Guid id, CancellationToken cancellationToken) =>
        dbContext.SearchPresets
            .SingleOrDefaultAsync(preset => preset.Id == id && preset.OwnerId == ownerId, cancellationToken);

    public void Add(SearchPreset preset) => dbContext.SearchPresets.Add(preset);

    public void Remove(SearchPreset preset) => dbContext.SearchPresets.Remove(preset);

    public async Task<SearchPresetSaveOutcome> SaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return SearchPresetSaveOutcome.Saved;
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException postgres
            && postgres.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            // The only unique constraint on this table is the per-owner name, so this is
            // unambiguous. The database's own message names the index and the offending
            // value; neither reaches the caller.
            dbContext.ChangeTracker.Clear();
            return SearchPresetSaveOutcome.NameConflict;
        }
    }
}
