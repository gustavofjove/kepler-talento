using KeplerTalento.Application.Abstractions.Identity;
using KeplerTalento.Application.Features.Positions;
using KeplerTalento.Application.Features.Search;
using MediatR;

namespace KeplerTalento.Web.Features.Positions;

public static class PositionEndpoints
{
    public sealed record PositionListRequest(string? Status, string? Text, int? Page, int? PageSize, string? SortField, string? SortDirection);
    public sealed record CreatePositionRequest(string? Title, string? Description, string? Location, SearchFiltersInput? Requirements);
    public sealed record UpdatePositionRequest(string? Title, string? Description, string? Location, string? Status, SearchFiltersInput? Requirements, uint Version);

    public static IEndpointRouteBuilder MapPositionEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/positions").WithTags("Positions");
        group.MapGet("/", async ([AsParameters] PositionListRequest request, ISender sender, CancellationToken cancellationToken) =>
                Results.Ok(await sender.Send(new ListPositionsQuery(request.Status, request.Text, request.Page, request.PageSize, request.SortField, request.SortDirection), cancellationToken)))
            .RequireAuthorization(Permissions.PositionsRead).WithName("ListPositions").Produces<PositionPageResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status401Unauthorized).ProducesProblem(StatusCodes.Status403Forbidden);
        group.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken cancellationToken) =>
                Results.Ok(await sender.Send(new GetPositionQuery(id), cancellationToken)))
            .RequireAuthorization(Permissions.PositionsRead).WithName("GetPosition").Produces<PositionResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized).ProducesProblem(StatusCodes.Status403Forbidden).ProducesProblem(StatusCodes.Status404NotFound);
        group.MapPost("/", async (CreatePositionRequest request, ISender sender, CancellationToken cancellationToken) =>
            {
                var created = await sender.Send(new CreatePositionCommand(request.Title, request.Description, request.Location, request.Requirements), cancellationToken);
                return Results.Created($"/api/positions/{created.Id}", created);
            }).RequireAuthorization(Permissions.PositionsManage).WithName("CreatePosition").Produces<PositionResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status401Unauthorized).ProducesProblem(StatusCodes.Status403Forbidden).ProducesProblem(StatusCodes.Status409Conflict);
        group.MapPut("/{id:guid}", async (Guid id, UpdatePositionRequest request, ISender sender, CancellationToken cancellationToken) =>
                Results.Ok(await sender.Send(new UpdatePositionCommand(id, request.Title, request.Description, request.Location, request.Status, request.Requirements, request.Version), cancellationToken)))
            .RequireAuthorization(Permissions.PositionsManage).WithName("UpdatePosition").Produces<PositionResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status401Unauthorized).ProducesProblem(StatusCodes.Status403Forbidden).ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict);
        return endpoints;
    }
}
