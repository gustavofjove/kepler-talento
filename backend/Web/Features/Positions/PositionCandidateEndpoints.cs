using KeplerTalento.Application.Features.Positions;
using MediatR;

namespace KeplerTalento.Web.Features.Positions;

/// <summary>
/// KTL-30 position candidate links. Kept apart from <see cref="PositionEndpoints"/> so that a
/// position itself still has no delete route: the only DELETE here removes one link.
/// </summary>
public static class PositionCandidateEndpoints
{
    public sealed record AddPositionCandidateRequest(Guid? CandidateId);
    public sealed record ChangePositionCandidateStageRequest(string? Stage, uint Version);

    /// <summary><c>positions.read</c> and <c>candidates.read</c>, checked before binding.</summary>
    public const string ReadPolicy = "positions.candidates.read";

    /// <summary><c>positions.manage</c> and <c>candidates.read</c>, checked before binding.</summary>
    public const string ManagePolicy = "positions.candidates.manage";

    public static IEndpointRouteBuilder MapPositionCandidateEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/positions/{id:guid}/candidates").WithTags("Positions");
        group.MapGet("/", async (Guid id, ISender sender, CancellationToken cancellationToken) =>
                Results.Ok(await sender.Send(new ListPositionCandidatesQuery(id), cancellationToken)))
            .RequireAuthorization(ReadPolicy).WithName("ListPositionCandidates").Produces<IReadOnlyList<PositionCandidateResponse>>()
            .ProducesProblem(StatusCodes.Status401Unauthorized).ProducesProblem(StatusCodes.Status403Forbidden).ProducesProblem(StatusCodes.Status404NotFound);
        group.MapPost("/", async (Guid id, AddPositionCandidateRequest request, ISender sender, CancellationToken cancellationToken) =>
            {
                var added = await sender.Send(new AddPositionCandidateCommand(id, request.CandidateId), cancellationToken);
                return Results.Created($"/api/positions/{id}/candidates/{added.CandidateId}", added);
            }).RequireAuthorization(ManagePolicy).WithName("AddPositionCandidate").Produces<PositionCandidateResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status401Unauthorized).ProducesProblem(StatusCodes.Status403Forbidden).ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict);
        group.MapPut("/{candidateId:guid}/stage", async (Guid id, Guid candidateId, ChangePositionCandidateStageRequest request, ISender sender, CancellationToken cancellationToken) =>
                Results.Ok(await sender.Send(new ChangePositionCandidateStageCommand(id, candidateId, request.Stage, request.Version), cancellationToken)))
            .RequireAuthorization(ManagePolicy).WithName("ChangePositionCandidateStage").Produces<PositionCandidateResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status401Unauthorized).ProducesProblem(StatusCodes.Status403Forbidden).ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict);
        group.MapDelete("/{candidateId:guid}", async (Guid id, Guid candidateId, ISender sender, CancellationToken cancellationToken) =>
            {
                await sender.Send(new RemovePositionCandidateCommand(id, candidateId), cancellationToken);
                return Results.NoContent();
            }).RequireAuthorization(ManagePolicy).WithName("RemovePositionCandidate").Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized).ProducesProblem(StatusCodes.Status403Forbidden).ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict);

        // The candidate-scoped read lives in the Positions slice, which owns the data and guards.
        endpoints.MapGet("/api/candidates/{candidateId:guid}/positions", async (Guid candidateId, ISender sender, CancellationToken cancellationToken) =>
                Results.Ok(await sender.Send(new ListCandidatePositionsQuery(candidateId), cancellationToken)))
            .WithTags("Positions").RequireAuthorization(ReadPolicy).WithName("ListCandidatePositions").Produces<IReadOnlyList<CandidatePositionResponse>>()
            .ProducesProblem(StatusCodes.Status401Unauthorized).ProducesProblem(StatusCodes.Status403Forbidden).ProducesProblem(StatusCodes.Status404NotFound);
        return endpoints;
    }
}
