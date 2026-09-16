using KeplerTalento.Application.Abstractions.Persistence;
using KeplerTalento.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace KeplerTalento.Infrastructure.Persistence;

/// <summary>Role persistence.</summary>
public sealed class RoleRepository(ApplicationDbContext dbContext) : IRoleRepository
{
    public async Task<IReadOnlyList<Role>> ListAsync(CancellationToken cancellationToken) =>
        await dbContext.Roles
            .AsNoTracking()
            .OrderBy(role => role.Name)
            .ToListAsync(cancellationToken);

    public Task<Role?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Roles.SingleOrDefaultAsync(role => role.Id == id, cancellationToken);

    public Task<Role?> FindByNameAsync(string name, CancellationToken cancellationToken) =>
        // No tracking: this is the per-request permission lookup, which never writes.
        dbContext.Roles.AsNoTracking().SingleOrDefaultAsync(role => role.Name == name, cancellationToken);

    public void Add(Role role) => dbContext.Roles.Add(role);

    public void ExpectVersion(Role role, uint version) =>
        dbContext.Entry(role).Property(entity => entity.Version).OriginalValue = version;

    public async Task<RoleSaveOutcome> SaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return RoleSaveOutcome.Saved;
        }
        catch (DbUpdateConcurrencyException)
        {
            dbContext.ChangeTracker.Clear();
            return RoleSaveOutcome.ConcurrencyConflict;
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException postgres
            && postgres.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            // The only unique constraint on ADM_Roles is the name, so this is unambiguous.
            // The database's message names the index and the value; neither reaches the caller.
            dbContext.ChangeTracker.Clear();
            return RoleSaveOutcome.NameConflict;
        }
    }
}
