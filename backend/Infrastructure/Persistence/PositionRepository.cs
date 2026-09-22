using KeplerTalento.Application.Abstractions.Correlation;
using KeplerTalento.Application.Abstractions.Identity;
using KeplerTalento.Application.Abstractions.Persistence;
using KeplerTalento.Domain.Auditing;
using KeplerTalento.Domain.Positions;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace KeplerTalento.Infrastructure.Persistence;

public sealed class PositionRepository(ApplicationDbContext dbContext, ICurrentActor actor, ICorrelationContext correlation)
    : IPositionRepository
{
    public async Task<PositionPage> ListAsync(PositionListOptions options, CancellationToken cancellationToken)
    {
        var query = dbContext.Positions.AsNoTracking();
        if (options.Status != "all") query = query.Where(position => position.Status == options.Status);
        if (options.Text.Length > 0) query = query.Where(position => position.NormalizedTitle.Contains(options.Text) || position.NormalizedLocation.Contains(options.Text));
        var total = await query.CountAsync(cancellationToken);
        var descending = options.SortDirection == "desc";
        query = options.SortField switch
        {
            "title" => descending ? query.OrderByDescending(x => x.NormalizedTitle).ThenBy(x => x.Id) : query.OrderBy(x => x.NormalizedTitle).ThenBy(x => x.Id),
            "location" => descending ? query.OrderByDescending(x => x.NormalizedLocation).ThenBy(x => x.Id) : query.OrderBy(x => x.NormalizedLocation).ThenBy(x => x.Id),
            "status" => descending ? query.OrderByDescending(x => x.Status).ThenBy(x => x.Id) : query.OrderBy(x => x.Status).ThenBy(x => x.Id),
            _ => descending ? query.OrderByDescending(x => x.UpdatedAtUtc).ThenBy(x => x.Id) : query.OrderBy(x => x.UpdatedAtUtc).ThenBy(x => x.Id),
        };
        var items = await query.Skip((options.Page - 1) * options.PageSize).Take(options.PageSize)
            .Select(x => new PositionSummary(x.Id, x.Title, x.Location, x.Status, x.UpdatedAtUtc, x.Version))
            .ToListAsync(cancellationToken);
        return new PositionPage(items, options.Page, options.PageSize, total);
    }

    public Task<Position?> FindAsync(Guid id, CancellationToken cancellationToken) => dbContext.Positions.SingleOrDefaultAsync(position => position.Id == id, cancellationToken);
    public void Add(Position position) => dbContext.Positions.Add(position);
    public void ExpectVersion(Position position, uint version) => dbContext.Entry(position).Property(x => x.Version).OriginalValue = version;

    public async Task<PositionSaveOutcome> SaveAsync(string auditEventType, CancellationToken cancellationToken)
    {
        var position = dbContext.ChangeTracker.Entries<Position>().Single().Entity;
        dbContext.AuditEvents.Add(new AuditEvent(Guid.CreateVersion7(), auditEventType, position.Id.ToString("N"), correlation.CorrelationId, DateTimeOffset.UtcNow, actor.ToAuditActor()));
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            await dbContext.Entry(position).ReloadAsync(cancellationToken);
            return PositionSaveOutcome.Saved;
        }
        catch (DbUpdateConcurrencyException) { dbContext.ChangeTracker.Clear(); return PositionSaveOutcome.ConcurrencyConflict; }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException postgres && postgres.SqlState == PostgresErrorCodes.UniqueViolation)
        { dbContext.ChangeTracker.Clear(); return PositionSaveOutcome.TitleConflict; }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException postgres && postgres.SqlState is PostgresErrorCodes.CheckViolation or PostgresErrorCodes.ForeignKeyViolation)
        { dbContext.ChangeTracker.Clear(); return PositionSaveOutcome.ConstraintViolation; }
    }
}
