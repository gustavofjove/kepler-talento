using System.Globalization;
using FluentValidation;
using KeplerTalento.Application.Abstractions.Identity;
using KeplerTalento.Application.Abstractions.Persistence;
using KeplerTalento.Domain.Auditing;
using MediatR;

namespace KeplerTalento.Application.Features.Audit;

/// <summary>
/// One page of the audit trail. Filters arrive as the wire strings and are parsed by the
/// validator, so a malformed value is a stable validation problem rather than a binding failure
/// or a silently narrower result.
/// </summary>
/// <param name="Actor">A user id, <c>system</c> or <c>unknown</c>.</param>
/// <param name="Subject">
/// A subject identifier. A candidate id also matches the document events recorded under that
/// candidate.
/// </param>
public sealed record ListAuditEventsQuery(
    string? From,
    string? To,
    string? EventType,
    string? Actor,
    string? Subject,
    int? Page,
    int? PageSize) : IRequest<AuditPageResponse>;

public sealed class ListAuditEventsValidator : AbstractValidator<ListAuditEventsQuery>
{
    public ListAuditEventsValidator()
    {
        RuleFor(query => query.From)
            .Must(value => AuditFilterParsing.TryParseDate(value, out _))
            .WithErrorCode(AuditErrors.DateInvalid)
            .WithMessage(AuditErrors.DateInvalidMessage);
        RuleFor(query => query.To)
            .Must(value => AuditFilterParsing.TryParseDate(value, out _))
            .WithErrorCode(AuditErrors.DateInvalid)
            .WithMessage(AuditErrors.DateInvalidMessage);
        RuleFor(query => query)
            .Must(query =>
                !AuditFilterParsing.TryParseDate(query.From, out var from)
                || !AuditFilterParsing.TryParseDate(query.To, out var to)
                || from is null
                || to is null
                || from <= to)
            .OverridePropertyName("From")
            .WithErrorCode(AuditErrors.DateRangeInvalid)
            .WithMessage(AuditErrors.DateRangeInvalidMessage);
        RuleFor(query => query.EventType)
            .Must(value => string.IsNullOrWhiteSpace(value) || AuditEventTypes.IsKnown(value.Trim()))
            .WithErrorCode(AuditErrors.EventTypeUnknown)
            .WithMessage(AuditErrors.EventTypeUnknownMessage);
        RuleFor(query => query.Actor)
            .Must(value => AuditFilterParsing.TryParseActor(value, out _, out _))
            .WithErrorCode(AuditErrors.ActorInvalid)
            .WithMessage(AuditErrors.ActorInvalidMessage);
        RuleFor(query => query.Subject)
            .Must(value => value is null || value.Trim().Length <= 100)
            .WithErrorCode(AuditErrors.SubjectInvalid)
            .WithMessage(AuditErrors.SubjectInvalidMessage);
        RuleFor(query => query.Page)
            .Must(value => value is null or >= 1)
            .WithErrorCode(AuditErrors.PageInvalid)
            .WithMessage(AuditErrors.PageInvalidMessage);
        RuleFor(query => query.PageSize)
            .Must(value => value is null or >= 1 and <= AuditPaging.MaximumPageSize)
            .WithErrorCode(AuditErrors.PageSizeInvalid)
            .WithMessage(AuditErrors.PageSizeInvalidMessage);
    }
}

public sealed class ListAuditEventsHandler(IAuditRepository audits, ICurrentActor actor)
    : IRequestHandler<ListAuditEventsQuery, AuditPageResponse>
{
    public async Task<AuditPageResponse> Handle(ListAuditEventsQuery request, CancellationToken cancellationToken)
    {
        AuditGuards.RequireRead(actor);

        AuditFilterParsing.TryParseDate(request.From, out var from);
        AuditFilterParsing.TryParseDate(request.To, out var to);
        AuditFilterParsing.TryParseActor(request.Actor, out var actorKind, out var actorUserId);
        var filter = new AuditFilter(
            from,
            to,
            string.IsNullOrWhiteSpace(request.EventType) ? null : request.EventType.Trim(),
            actorKind,
            actorUserId,
            string.IsNullOrWhiteSpace(request.Subject) ? null : request.Subject.Trim());

        var page = await audits.ListAsync(
            filter,
            request.Page ?? AuditPaging.DefaultPage,
            request.PageSize ?? AuditPaging.DefaultPageSize,
            cancellationToken);
        return new AuditPageResponse(
            [.. page.Items.Select(AuditEventResponse.From)],
            page.Page,
            page.PageSize,
            page.TotalCount);
    }
}

internal static class AuditFilterParsing
{
    /// <summary>True for an absent value or an ISO 8601 date/time; a value without an offset is UTC.</summary>
    public static bool TryParseDate(string? value, out DateTimeOffset? parsed)
    {
        parsed = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }
        if (DateTimeOffset.TryParse(
                value.Trim(),
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var result))
        {
            parsed = result.ToUniversalTime();
            return true;
        }
        return false;
    }

    /// <summary>True for an absent value, a non-empty user id, <c>system</c> or <c>unknown</c>.</summary>
    public static bool TryParseActor(string? value, out AuditActorKind? kind, out Guid? userId)
    {
        kind = null;
        userId = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }
        var trimmed = value.Trim();
        if (trimmed == AuditActorKinds.System)
        {
            kind = AuditActorKind.System;
            return true;
        }
        if (trimmed == AuditActorKinds.Unknown)
        {
            kind = AuditActorKind.Unknown;
            return true;
        }
        if (Guid.TryParse(trimmed, out var id) && id != Guid.Empty)
        {
            kind = AuditActorKind.User;
            userId = id;
            return true;
        }
        return false;
    }
}
