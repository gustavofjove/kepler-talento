namespace KeplerTalento.Domain.Operations;

public enum OperationStatus
{
    Queued,
    Running,
    Completed,
    Failed,
    Cancelled,
}

public sealed class Operation
{
    private Operation() { }

    public Operation(Guid id, string type, string correlationId, string idempotencyKey, DateTimeOffset now)
    {
        Id = id;
        Type = type;
        CorrelationId = correlationId;
        IdempotencyKey = idempotencyKey;
        CreatedAtUtc = now;
        UpdatedAtUtc = now;
    }

    public Guid Id { get; private set; }
    public string Type { get; private set; } = string.Empty;
    public OperationStatus Status { get; private set; } = OperationStatus.Queued;
    public string CorrelationId { get; private set; } = string.Empty;
    public string IdempotencyKey { get; private set; } = string.Empty;
    public int AttemptCount { get; private set; }
    public int MaxAttempts { get; private set; } = 3;
    public string? Owner { get; private set; }
    public DateTimeOffset? LeaseExpiresAtUtc { get; private set; }
    public string? OutcomeCode { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public uint Version { get; private set; }

    public bool TryClaim(string owner, DateTimeOffset now, TimeSpan lease)
    {
        if (Status == OperationStatus.Running && LeaseExpiresAtUtc > now)
        {
            return false;
        }
        if (Status != OperationStatus.Queued && Status != OperationStatus.Running)
        {
            return false;
        }
        Status = OperationStatus.Running;
        Owner = owner;
        LeaseExpiresAtUtc = now.Add(lease);
        AttemptCount++;
        UpdatedAtUtc = now;
        return true;
    }

    public bool Complete(string owner, string outcomeCode, DateTimeOffset now)
    {
        if (Status == OperationStatus.Completed)
        {
            return true;
        }
        if (Status != OperationStatus.Running || !string.Equals(Owner, owner, StringComparison.Ordinal))
        {
            return false;
        }
        Status = OperationStatus.Completed;
        OutcomeCode = outcomeCode;
        LeaseExpiresAtUtc = null;
        UpdatedAtUtc = now;
        return true;
    }

    public bool Fail(string owner, string outcomeCode, DateTimeOffset now)
    {
        if (Status != OperationStatus.Running || !string.Equals(Owner, owner, StringComparison.Ordinal))
        {
            return false;
        }
        Status = OperationStatus.Failed;
        OutcomeCode = outcomeCode;
        LeaseExpiresAtUtc = null;
        UpdatedAtUtc = now;
        return true;
    }

    public bool Cancel(string? owner, string outcomeCode, DateTimeOffset now)
    {
        if (Status == OperationStatus.Cancelled)
        {
            return true;
        }
        if (Status is OperationStatus.Completed or OperationStatus.Failed ||
            (Status == OperationStatus.Running && !string.Equals(Owner, owner, StringComparison.Ordinal)))
        {
            return false;
        }
        Status = OperationStatus.Cancelled;
        OutcomeCode = outcomeCode;
        Owner = null;
        LeaseExpiresAtUtc = null;
        UpdatedAtUtc = now;
        return true;
    }
}
