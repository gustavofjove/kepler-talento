using KeplerTalento.Domain.Auditing;

namespace KeplerTalento.Application.Abstractions.Persistence;

/// <summary>A validated filter over the audit trail. Null members are not applied.</summary>
/// <param name="ActorKind">Restricts to one actor kind; combined with <paramref name="ActorUserId"/> for users.</param>
public sealed record AuditFilter(
    DateTimeOffset? FromUtc,
    DateTimeOffset? ToUtc,
    string? EventType,
    AuditActorKind? ActorKind,
    Guid? ActorUserId,
    string? SubjectId);

public sealed record AuditPage(IReadOnlyList<AuditEvent> Items, int Page, int PageSize, int TotalCount);

/// <summary>
/// Reads the audit trail and records events that have no business write to join.
/// </summary>
/// <remarks>
/// There is deliberately no update or delete member, and <c>ktl_runtime</c> holds neither
/// privilege on the table. Write events are still added by the feature repositories inside the
/// transaction of the change they describe; <see cref="RecordAsync"/> is for reads, which have no
/// such transaction (KTL-19 design D5).
/// </remarks>
public interface IAuditRepository
{
    Task RecordAsync(AuditEvent auditEvent, CancellationToken cancellationToken);

    /// <summary>Ordered by creation time descending, then id descending, so paging is stable.</summary>
    Task<AuditPage> ListAsync(AuditFilter filter, int page, int pageSize, CancellationToken cancellationToken);
}
