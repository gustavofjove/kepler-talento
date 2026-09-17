using KeplerTalento.Domain.Auditing;

namespace KeplerTalento.Application.Features.Audit;

/// <summary>
/// One audit event on the wire: identifiers and codes only.
/// </summary>
/// <remarks>
/// No candidate name, no actor display name, no email and no external subject (KTL-19 design D9).
/// The client resolves <paramref name="ActorUserId"/> through the users endpoint when it is
/// entitled to; the subject is resolved, if at all, by the candidate endpoint behind its own
/// permission.
/// </remarks>
/// <param name="ActorKind"><c>user</c>, <c>system</c> or <c>unknown</c> — never absent.</param>
public sealed record AuditEventResponse(
    Guid Id,
    string EventType,
    string SubjectId,
    string ActorKind,
    Guid? ActorUserId,
    string? OutcomeCode,
    DateTimeOffset CreatedAt)
{
    public static AuditEventResponse From(AuditEvent audit)
    {
        var actor = audit.Actor;
        return new(
            audit.Id,
            audit.EventType,
            audit.SubjectId,
            AuditActorKinds.ToWire(actor.Kind),
            actor.UserId,
            audit.OutcomeCode,
            audit.CreatedAtUtc);
    }
}

public sealed record AuditPageResponse(IReadOnlyList<AuditEventResponse> Items, int Page, int PageSize, int TotalCount);

/// <summary>The actor kinds as the API spells them.</summary>
public static class AuditActorKinds
{
    public const string User = "user";
    public const string System = "system";
    public const string Unknown = "unknown";

    public static string ToWire(AuditActorKind kind) => kind switch
    {
        AuditActorKind.User => User,
        AuditActorKind.System => System,
        _ => Unknown,
    };
}

public static class AuditPaging
{
    public const int DefaultPage = 1;
    public const int DefaultPageSize = 25;
    public const int MaximumPageSize = 100;
}

/// <summary>Stable error codes for the audit read surface.</summary>
public static class AuditErrors
{
    public const string EventTypeUnknown = "audit.eventType.unknown";
    public const string DateInvalid = "audit.date.invalid";
    public const string DateRangeInvalid = "audit.dateRange.invalid";
    public const string ActorInvalid = "audit.actor.invalid";
    public const string SubjectInvalid = "audit.subject.invalid";
    public const string PageInvalid = "audit.page.invalid";
    public const string PageSizeInvalid = "audit.pageSize.invalid";

    public const string EventTypeUnknownMessage = "El tipo de evento no forma parte del catálogo de auditoría.";
    public const string DateInvalidMessage = "La fecha no tiene un formato ISO 8601 válido.";
    public const string DateRangeInvalidMessage = "La fecha inicial no puede ser posterior a la fecha final.";
    public const string ActorInvalidMessage = "El actor debe ser un identificador de usuario, «system» o «unknown».";
    public const string SubjectInvalidMessage = "El sujeto no es válido.";
    public const string PageInvalidMessage = "La página debe ser 1 o superior.";
    public const string PageSizeInvalidMessage = "El tamaño de página debe estar entre 1 y 100.";
}
