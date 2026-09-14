using KeplerTalento.Application.Abstractions.Persistence;
using KeplerTalento.Domain.Search;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace KeplerTalento.Infrastructure.Persistence;

/// <summary>Saved-search persistence for the shared library.</summary>
public sealed class SearchPresetRepository(ApplicationDbContext dbContext) : ISearchPresetRepository
{
    public async Task<IReadOnlyList<SearchPreset>> ListAsync(CancellationToken cancellationToken) =>
        await dbContext.SearchPresets
            .AsNoTracking()
            // The normalized name is what makes the listing alphabetical without regard to case
            // or accents, and it is the column the unique index is sorted by, so the order costs
            // nothing.
            .OrderBy(preset => preset.NormalizedName)
            .ToListAsync(cancellationToken);

    public Task<SearchPreset?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.SearchPresets.SingleOrDefaultAsync(preset => preset.Id == id, cancellationToken);

    public void Add(SearchPreset preset) => dbContext.SearchPresets.Add(preset);

    public void Remove(SearchPreset preset) => dbContext.SearchPresets.Remove(preset);

    // A version above int.MaxValue was never issued, so wrapping it can only fail to match —
    // which is the correct answer for it.
    public void ExpectVersion(SearchPreset preset, uint version) =>
        dbContext.Entry(preset).Property(entity => entity.Version).OriginalValue = unchecked((int)version);

    public async Task<SearchPresetSaveOutcome> SaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return SearchPresetSaveOutcome.Saved;
        }
        catch (DbUpdateConcurrencyException)
        {
            dbContext.ChangeTracker.Clear();
            return SearchPresetSaveOutcome.ConcurrencyConflict;
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException postgres
            && postgres.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            // The only unique constraint on this table is the name, so this is unambiguous.
            // The database's own message names the index and the offending value; neither
            // reaches the caller.
            dbContext.ChangeTracker.Clear();
            return SearchPresetSaveOutcome.NameConflict;
        }
    }
}
