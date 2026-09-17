using KeplerTalento.Application.Abstractions.Persistence;
using KeplerTalento.Domain.Auditing;
using Microsoft.EntityFrameworkCore;

namespace KeplerTalento.Infrastructure.Persistence;

public sealed class AuditRepository(ApplicationDbContext dbContext) : IAuditRepository
{
    public async Task RecordAsync(AuditEvent auditEvent, CancellationToken cancellationToken)
    {
        dbContext.AuditEvents.Add(auditEvent);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<AuditPage> ListAsync(
        AuditFilter filter,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query = dbContext.AuditEvents.AsNoTracking();
        if (filter.FromUtc is { } from)
        {
            query = query.Where(audit => audit.CreatedAtUtc >= from);
        }
        if (filter.ToUtc is { } to)
        {
            query = query.Where(audit => audit.CreatedAtUtc <= to);
        }
        if (filter.EventType is { } eventType)
        {
            query = query.Where(audit => audit.EventType == eventType);
        }
        query = filter.ActorKind switch
        {
            AuditActorKind.User => query.Where(audit =>
                audit.ActorKind == AuditEvent.UserActorKind && audit.ActorUserId == filter.ActorUserId),
            AuditActorKind.System => query.Where(audit => audit.ActorKind == AuditEvent.SystemActorKind),
            AuditActorKind.Unknown => query.Where(audit => audit.ActorKind == null),
            _ => query,
        };
        if (filter.SubjectId is { } subject)
        {
            // Document events are recorded under "candidate:<id>;document:<id>", so a candidate id
            // also finds that candidate's document events.
            if (Guid.TryParse(subject, out var candidateId))
            {
                var compact = candidateId.ToString("N");
                var documentPrefix = $"candidate:{compact};";
                query = query.Where(audit =>
                    audit.SubjectId == subject
                    || audit.SubjectId == compact
                    || audit.SubjectId.StartsWith(documentPrefix));
            }
            else
            {
                query = query.Where(audit => audit.SubjectId == subject);
            }
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(audit => audit.CreatedAtUtc)
            .ThenByDescending(audit => audit.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
        return new AuditPage(items, page, pageSize, totalCount);
    }
}
