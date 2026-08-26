using KeplerTalento.Application.Abstractions.Identity;
using KeplerTalento.Application.Common.Errors;
using KeplerTalento.Application.Features.Catalogs;
using MediatR;

namespace KeplerTalento.Web.Features.Catalogs;

/// <summary>
/// Catalog administration. There is deliberately no DELETE verb: a catalog value is
/// removed from use by being deactivated, never by being physically deleted.
/// </summary>
public static class CatalogEndpoints
{
    public sealed record CreateCatalogItemRequest(string NameEs, string? Code, string? NameEn);

    public sealed record UpdateCatalogItemRequest(string NameEs, string? Code, string? NameEn, uint Version);

    public sealed record ReorderCatalogFamilyRequest(IReadOnlyList<Guid> OrderedIds);

    public sealed record SetCatalogItemActiveRequest(bool IsActive, uint Version);

    public static IEndpointRouteBuilder MapCatalogEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/catalogs").WithTags("Catalogs");

        group.MapGet("/{family}", async (
                string family,
                bool? includeInactive,
                ISender sender,
                ICurrentActor actor,
                CancellationToken cancellationToken) =>
            {
                // Authorize before the request is validated, so an unauthorized caller cannot
                // probe the family set through validation problems.
                if (!actor.IsAuthenticated || !actor.HasPermission(Permissions.CatalogsRead))
                {
                    throw new ForbiddenException();
                }
                var items = await sender.Send(
                    new ListCatalogFamilyQuery(family, includeInactive ?? false),
                    cancellationToken);
                return Results.Ok(items);
            })
            .WithName("ListCatalogFamily")
            .Produces<IReadOnlyList<CatalogItemResponse>>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapPost("/{family}", async (
                string family,
                CreateCatalogItemRequest request,
                ISender sender,
                ICurrentActor actor,
                CancellationToken cancellationToken) =>
            {
                if (!Manages(actor)) throw new ForbiddenException();
                var created = await sender.Send(
                    new CreateCatalogItemCommand(family, request.NameEs, request.Code, request.NameEn),
                    cancellationToken);
                return Results.Created($"/api/catalogs/{family}/{created.Id}", created);
            })
            .WithName("CreateCatalogItem")
            .Produces<CatalogItemResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapPut("/{family}/order", async (
                string family,
                ReorderCatalogFamilyRequest request,
                ISender sender,
                ICurrentActor actor,
                CancellationToken cancellationToken) =>
            {
                if (!Manages(actor)) throw new ForbiddenException();
                var items = await sender.Send(
                    new ReorderCatalogFamilyCommand(family, request.OrderedIds ?? []),
                    cancellationToken);
                return Results.Ok(items);
            })
            .WithName("ReorderCatalogFamily")
            .Produces<IReadOnlyList<CatalogItemResponse>>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPut("/{family}/{id:guid}", async (
                string family,
                Guid id,
                UpdateCatalogItemRequest request,
                ISender sender,
                ICurrentActor actor,
                CancellationToken cancellationToken) =>
            {
                if (!Manages(actor)) throw new ForbiddenException();
                var updated = await sender.Send(
                    new UpdateCatalogItemCommand(family, id, request.NameEs, request.Code, request.NameEn, request.Version),
                    cancellationToken);
                return Results.Ok(updated);
            })
            .WithName("UpdateCatalogItem")
            .Produces<CatalogItemResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPut("/{family}/{id:guid}/active", async (
                string family,
                Guid id,
                SetCatalogItemActiveRequest request,
                ISender sender,
                ICurrentActor actor,
                CancellationToken cancellationToken) =>
            {
                if (!Manages(actor)) throw new ForbiddenException();
                var updated = await sender.Send(
                    new SetCatalogItemActiveCommand(family, id, request.IsActive, request.Version),
                    cancellationToken);
                return Results.Ok(updated);
            })
            .WithName("SetCatalogItemActive")
            .Produces<CatalogItemResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return endpoints;
    }

    private static bool Manages(ICurrentActor actor) =>
        actor.IsAuthenticated && actor.HasPermission(Permissions.CatalogsManage);
}
