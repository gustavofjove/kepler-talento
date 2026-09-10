using KeplerTalento.Application.Abstractions.Correlation;
using KeplerTalento.Application.Abstractions.Identity;
using KeplerTalento.Application.Abstractions.Persistence;
using KeplerTalento.Domain.Auditing;
using KeplerTalento.Domain.Catalogs;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace KeplerTalento.Infrastructure.Persistence;

public sealed class CatalogRepository(
    ApplicationDbContext dbContext,
    ICorrelationContext correlation,
    ICurrentActor actor) : ICatalogRepository
{
    public async Task<IReadOnlyList<CatalogItem>> ListAsync(
        string family,
        bool includeInactive,
        CancellationToken cancellationToken)
    {
        var query = dbContext.CatalogItems.Where(item => item.Family == family);
        if (!includeInactive)
        {
            query = query.Where(item => item.IsActive);
        }
        return await query
            .OrderBy(item => item.SortOrder)
            .ThenBy(item => item.NameEs)
            .ToListAsync(cancellationToken);
    }

    public Task<CatalogItem?> FindAsync(string family, Guid id, CancellationToken cancellationToken) =>
        dbContext.CatalogItems.SingleOrDefaultAsync(
            item => item.Family == family && item.Id == id,
            cancellationToken);

    public void Add(CatalogItem item) => dbContext.CatalogItems.Add(item);

    public void ExpectVersion(CatalogItem item, uint version) =>
        dbContext.Entry(item).Property(entity => entity.Version).OriginalValue = version;

    public async Task<CatalogSaveOutcome> SaveAsync(
        string auditEventType,
        string subjectId,
        CancellationToken cancellationToken)
    {
        dbContext.AuditEvents.Add(new AuditEvent(
            Guid.CreateVersion7(),
            auditEventType,
            subjectId,
            correlation.CorrelationId,
            DateTimeOffset.UtcNow,
            actor.ExternalKey));
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return CatalogSaveOutcome.Saved;
        }
        catch (DbUpdateConcurrencyException)
        {
            return CatalogSaveOutcome.ConcurrencyConflict;
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException postgres
            && postgres.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            return postgres.ConstraintName == "UX_CAT_CatalogItems_Family_Code"
                ? CatalogSaveOutcome.DuplicateCode
                : CatalogSaveOutcome.DuplicateName;
        }
    }
}
