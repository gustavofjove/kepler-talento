using KeplerTalento.Application.Abstractions.Identity;
using KeplerTalento.Application.Features.Audit;
using MediatR;

namespace KeplerTalento.Web.Features.Audit;

/// <summary>
/// The audit read surface (KTL-19).
/// </summary>
/// <remarks>
/// Read-only by design: there is no verb here, or anywhere, that changes or removes an audit event,
/// and <c>ktl_runtime</c> holds neither <c>UPDATE</c> nor <c>DELETE</c> on the table.
///
/// The group requires <see cref="Permissions.AuditRead"/> as an endpoint policy, which runs before
/// query binding and validation, so an unauthorized caller with a malformed filter gets the same
/// refusal as one with a valid filter. The handler repeats the guard.
/// </remarks>
public static class AuditEndpoints
{
    public sealed record AuditListRequest(
        string? From,
        string? To,
        string? EventType,
        string? Actor,
        string? Subject,
        int? Page,
        int? PageSize);

    public static IEndpointRouteBuilder MapAuditEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/audit")
            .WithTags("Audit")
            .RequireAuthorization(Permissions.AuditRead);

        group.MapGet("/events", async (
                [AsParameters] AuditListRequest request,
                ISender sender,
                CancellationToken cancellationToken) =>
                Results.Ok(await sender.Send(
                    new ListAuditEventsQuery(
                        request.From,
                        request.To,
                        request.EventType,
                        request.Actor,
                        request.Subject,
                        request.Page,
                        request.PageSize),
                    cancellationToken)))
            .WithName("ListAuditEvents")
            .Produces<AuditPageResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        return endpoints;
    }
}
