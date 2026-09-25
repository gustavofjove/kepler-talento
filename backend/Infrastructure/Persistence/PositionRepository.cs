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
            .Select(x => new PositionSummary(x.Id, x.Title, x.Location, x.Status, x.UpdatedAtUtc, x.Version,
                // KTL-30: a count only, never names, so it needs no candidate permission.
                dbContext.PositionCandidates.Count(link => link.PositionId == x.Id)))
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

    public async Task<IReadOnlyList<PositionCandidateItem>> ListCandidatesAsync(Guid positionId, CancellationToken cancellationToken) =>
        await CandidateItems(positionId, candidateId: null).ToListAsync(cancellationToken);

    public Task<PositionCandidateItem?> FindCandidateItemAsync(Guid positionId, Guid candidateId, CancellationToken cancellationToken) =>
        CandidateItems(positionId, candidateId).SingleOrDefaultAsync(cancellationToken);

    private IQueryable<PositionCandidateItem> CandidateItems(Guid positionId, Guid? candidateId) =>
        from link in dbContext.PositionCandidates.AsNoTracking()
        join candidate in dbContext.Candidates.AsNoTracking() on link.CandidateId equals candidate.Id
        where link.PositionId == positionId && (candidateId == null || link.CandidateId == candidateId)
        // Stage vocabulary order (PositionCandidateStages.All), then newest first.
        orderby (link.Stage == PositionCandidateStages.New ? 0
            : link.Stage == PositionCandidateStages.Shortlisted ? 1
            : link.Stage == PositionCandidateStages.Interview ? 2
            : link.Stage == PositionCandidateStages.Hired ? 3 : 4),
            link.AddedAtUtc descending, link.CandidateId
        select new PositionCandidateItem(
            link.CandidateId, candidate.FirstName, candidate.LastName, candidate.Email, candidate.Phone,
            dbContext.Documents.Any(document => document.CandidateId == candidate.Id && document.IsPrimary),
            candidate.IsActive,
            link.Stage, link.AddedAtUtc, link.UpdatedAtUtc, link.Version);

    public async Task<IReadOnlyList<CandidatePositionItem>> ListForCandidateAsync(Guid candidateId, CancellationToken cancellationToken) =>
        await (from link in dbContext.PositionCandidates.AsNoTracking()
               join position in dbContext.Positions.AsNoTracking() on link.PositionId equals position.Id
               where link.CandidateId == candidateId
               orderby (position.Status == PositionStatuses.Open ? 0 : 1), link.AddedAtUtc descending, link.PositionId
               select new CandidatePositionItem(
                   link.PositionId, position.Title, position.Status,
                   link.Stage, link.AddedAtUtc, link.UpdatedAtUtc, link.Version))
            .ToListAsync(cancellationToken);

    public async Task<bool?> IsPositionOpenAsync(Guid positionId, CancellationToken cancellationToken)
    {
        var status = await dbContext.Positions.AsNoTracking().Where(x => x.Id == positionId)
            .Select(x => x.Status).SingleOrDefaultAsync(cancellationToken);
        return status is null ? null : status == PositionStatuses.Open;
    }

    public async Task<bool?> IsCandidateActiveAsync(Guid candidateId, CancellationToken cancellationToken)
    {
        var states = await dbContext.Candidates.AsNoTracking().Where(x => x.Id == candidateId)
            .Select(x => x.IsActive).ToListAsync(cancellationToken);
        return states.Count == 0 ? null : states[0];
    }

    public Task<PositionCandidate?> FindLinkAsync(Guid positionId, Guid candidateId, CancellationToken cancellationToken) =>
        dbContext.PositionCandidates.SingleOrDefaultAsync(x => x.PositionId == positionId && x.CandidateId == candidateId, cancellationToken);

    public Task<int> CountLinksAsync(Guid positionId, CancellationToken cancellationToken) =>
        dbContext.PositionCandidates.CountAsync(x => x.PositionId == positionId, cancellationToken);

    public void AddLink(PositionCandidate link) => dbContext.PositionCandidates.Add(link);
    public void RemoveLink(PositionCandidate link) => dbContext.PositionCandidates.Remove(link);
    public void ExpectLinkVersion(PositionCandidate link, uint version) => dbContext.Entry(link).Property(x => x.Version).OriginalValue = version;

    public async Task<PositionSaveOutcome> SaveLinkAsync(string auditEventType, PositionCandidate link, CancellationToken cancellationToken)
    {
        dbContext.AuditEvents.Add(new AuditEvent(
            Guid.CreateVersion7(), auditEventType, PositionCandidate.AuditSubject(link.PositionId, link.CandidateId),
            correlation.CorrelationId, DateTimeOffset.UtcNow, actor.ToAuditActor()));
        var removing = dbContext.Entry(link).State == EntityState.Deleted;
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            if (!removing) await dbContext.Entry(link).ReloadAsync(cancellationToken);
            return PositionSaveOutcome.Saved;
        }
        catch (DbUpdateConcurrencyException) { dbContext.ChangeTracker.Clear(); return PositionSaveOutcome.ConcurrencyConflict; }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException postgres && postgres.SqlState == PostgresErrorCodes.UniqueViolation)
        { dbContext.ChangeTracker.Clear(); return PositionSaveOutcome.AlreadyLinked; }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException postgres && postgres.SqlState is PostgresErrorCodes.CheckViolation or PostgresErrorCodes.ForeignKeyViolation)
        { dbContext.ChangeTracker.Clear(); return PositionSaveOutcome.ConstraintViolation; }
    }
}
