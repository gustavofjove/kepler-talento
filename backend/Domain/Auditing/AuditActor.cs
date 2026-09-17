namespace KeplerTalento.Domain.Auditing;

/// <summary>
/// Who caused an audit event: a stored user, the system, or — only for rows written before
/// KTL-19 — nobody known.
/// </summary>
/// <remarks>
/// <para>
/// Writing code can produce <see cref="User"/> and <see cref="System"/> and nothing else. The
/// unknown case has no public way in: it exists only as what <see cref="AuditEvent.Actor"/>
/// reports for a historic row whose actor columns are null. That is what keeps "unknown" an honest
/// label — a null actor can only mean the row predates the actor being carried (design D2).
/// </para>
/// <para>
/// The user case carries the internal user id and nothing else: never an email, a display name
/// or the identity provider's subject.
/// </para>
/// </remarks>
public sealed class AuditActor : IEquatable<AuditActor>
{
    private AuditActor(AuditActorKind kind, Guid? userId)
    {
        Kind = kind;
        UserId = userId;
    }

    public AuditActorKind Kind { get; }

    /// <summary>The internal user id; set only for <see cref="AuditActorKind.User"/>.</summary>
    public Guid? UserId { get; }

    public static AuditActor System { get; } = new(AuditActorKind.System, null);

    public static AuditActor User(Guid userId)
    {
        if (userId == Guid.Empty) throw new ArgumentException("An audit actor needs a real user id.", nameof(userId));
        return new AuditActor(AuditActorKind.User, userId);
    }

    /// <summary>
    /// Deliberately internal: only <see cref="AuditEvent"/> produces it, when materialising a row
    /// that carries no actor.
    /// </summary>
    internal static AuditActor Unknown { get; } = new(AuditActorKind.Unknown, null);

    public bool Equals(AuditActor? other) => other is not null && other.Kind == Kind && other.UserId == UserId;

    public override bool Equals(object? obj) => Equals(obj as AuditActor);

    public override int GetHashCode() => HashCode.Combine(Kind, UserId);

    public override string ToString() => Kind == AuditActorKind.User ? $"user:{UserId:N}" : Kind.ToString().ToLowerInvariant();
}

public enum AuditActorKind
{
    Unknown,
    User,
    System,
}
