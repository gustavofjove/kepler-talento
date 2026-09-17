namespace KeplerTalento.Domain.Auditing;

/// <summary>
/// One row of the audit trail: identifiers, a catalogued type, an outcome code and a timestamp.
/// </summary>
/// <remarks>
/// The actor is a required constructor argument rather than something read ambiently, so an
/// audited operation that has no actor in scope fails to compile instead of silently writing an
/// actorless row (KTL-19 design D1). The event type must come from
/// <see cref="AuditEventTypes.All"/>.
/// </remarks>
public sealed class AuditEvent
{
    public const string UserActorKind = "user";
    public const string SystemActorKind = "system";

    private AuditEvent() { }

    public AuditEvent(
        Guid id,
        string eventType,
        string subjectId,
        string correlationId,
        DateTimeOffset createdAtUtc,
        AuditActor actor,
        string? outcomeCode = null)
    {
        ArgumentNullException.ThrowIfNull(actor);
        if (actor.Kind == AuditActorKind.Unknown)
        {
            throw new ArgumentException("An audit event cannot be written with an unknown actor.", nameof(actor));
        }
        if (!AuditEventTypes.IsKnown(eventType))
        {
            throw new ArgumentException($"'{eventType}' is not a catalogued audit event type.", nameof(eventType));
        }

        Id = id;
        EventType = eventType;
        SubjectId = subjectId;
        CorrelationId = correlationId;
        CreatedAtUtc = createdAtUtc;
        ActorKind = actor.Kind == AuditActorKind.System ? SystemActorKind : UserActorKind;
        ActorUserId = actor.UserId;
        OutcomeCode = outcomeCode;
    }

    public Guid Id { get; private set; }
    public string EventType { get; private set; } = string.Empty;
    public string SubjectId { get; private set; } = string.Empty;
    public string CorrelationId { get; private set; } = string.Empty;
    public string? OutcomeCode { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    /// <summary><c>user</c>, <c>system</c>, or null on a row written before KTL-19.</summary>
    public string? ActorKind { get; private set; }

    public Guid? ActorUserId { get; private set; }

    public AuditActor Actor => ActorKind switch
    {
        UserActorKind when ActorUserId is { } userId => AuditActor.User(userId),
        SystemActorKind => AuditActor.System,
        _ => AuditActor.Unknown,
    };
}
