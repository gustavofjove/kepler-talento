namespace KeplerTalento.Domain.Auditing;

public sealed class AuditEvent
{
    private AuditEvent() { }

    public AuditEvent(
        Guid id,
        string eventType,
        string subjectId,
        string correlationId,
        DateTimeOffset createdAtUtc,
        string? outcomeCode = null)
    {
        Id = id;
        EventType = eventType;
        SubjectId = subjectId;
        CorrelationId = correlationId;
        CreatedAtUtc = createdAtUtc;
        OutcomeCode = outcomeCode;
    }

    public Guid Id { get; private set; }
    public string EventType { get; private set; } = string.Empty;
    public string SubjectId { get; private set; } = string.Empty;
    public string CorrelationId { get; private set; } = string.Empty;
    public string? OutcomeCode { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
}
