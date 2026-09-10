using KeplerTalento.Application.Abstractions.Identity;
using KeplerTalento.Application.Common.Errors;
using KeplerTalento.Application.Features.Search;
using MediatR;

namespace KeplerTalento.Web.Features.Search;

/// <summary>
/// Candidate search and the saved searches that drive it.
/// </summary>
/// <remarks>
/// Search is a POST even though it reads nothing but data. The filter value carries nested
/// criteria pairs that a query string expresses badly, and — the deciding reason — search
/// terms are personal data that would otherwise appear in every proxy and access log that
/// records a URL. POST does not make the operation stateful: the handler writes nothing.
///
/// Every route authorizes before dispatching, as the candidate group does, so an
/// unauthorized caller cannot distinguish a valid request from an invalid one, nor an
/// existing preset from a missing one, by the shape of the refusal.
/// </remarks>
public static class SearchEndpoints
{
    public sealed record SearchCandidatesRequest(SearchFiltersInput? Filters, int? Page, int? PageSize);

    public sealed record SearchPresetRequest(string? Name, SearchFiltersInput? Filters);

    public static IEndpointRouteBuilder MapSearchEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/candidates/search", async (
                SearchCandidatesRequest request,
                ISender sender,
                ICurrentActor actor,
                CancellationToken cancellationToken) =>
            {
                Require(actor);
                return Results.Ok(await sender.Send(
                    new SearchCandidatesQuery(request.Filters, request.Page, request.PageSize),
                    cancellationToken));
            })
            .WithTags("Candidates")
            .WithName("SearchCandidates")
            .Produces<SearchPage<CandidateSearchItem>>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        var presets = endpoints.MapGroup("/api/search-presets").WithTags("SearchPresets");

        presets.MapGet("/", async (ISender sender, ICurrentActor actor, CancellationToken cancellationToken) =>
            {
                Require(actor);
                return Results.Ok(await sender.Send(new ListSearchPresetsQuery(), cancellationToken));
            })
            .WithName("ListSearchPresets")
            .Produces<IReadOnlyList<SearchPresetResponse>>()
            .ProducesProblem(StatusCodes.Status403Forbidden);

        presets.MapPost("/", async (
                SearchPresetRequest request,
                ISender sender,
                ICurrentActor actor,
                CancellationToken cancellationToken) =>
            {
                Require(actor);
                var created = await sender.Send(
                    new CreateSearchPresetCommand(request.Name, request.Filters),
                    cancellationToken);
                return Results.Created($"/api/search-presets/{created.Id}", created);
            })
            .WithName("CreateSearchPreset")
            .Produces<SearchPresetResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict);

        presets.MapPut("/{id:guid}", async (
                Guid id,
                SearchPresetRequest request,
                ISender sender,
                ICurrentActor actor,
                CancellationToken cancellationToken) =>
            {
                Require(actor);
                return Results.Ok(await sender.Send(
                    new UpdateSearchPresetCommand(id, request.Name, request.Filters),
                    cancellationToken));
            })
            .WithName("UpdateSearchPreset")
            .WithPresetResponses()
            .ProducesProblem(StatusCodes.Status409Conflict);

        presets.MapDelete("/{id:guid}", async (
                Guid id,
                ISender sender,
                ICurrentActor actor,
                CancellationToken cancellationToken) =>
            {
                Require(actor);
                await sender.Send(new DeleteSearchPresetCommand(id), cancellationToken);
                return Results.NoContent();
            })
            .WithName("DeleteSearchPreset")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        // Applying a preset changes its last-used time, so it is a POST on a sub-resource
        // rather than a GET that quietly writes.
        presets.MapPost("/{id:guid}/use", async (
                Guid id,
                ISender sender,
                ICurrentActor actor,
                CancellationToken cancellationToken) =>
            {
                Require(actor);
                return Results.Ok(await sender.Send(new UseSearchPresetCommand(id), cancellationToken));
            })
            .WithName("UseSearchPreset")
            .WithPresetResponses();

        return endpoints;
    }

    private static RouteHandlerBuilder WithPresetResponses(this RouteHandlerBuilder builder) =>
        builder
            .Produces<SearchPresetResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

    /// <summary>
    /// Reading candidates is what searching and saving a search require. There is
    /// deliberately no consultation of <c>view_all_candidates</c>: see the KTL-10 design —
    /// the domain models no ownership or team from which a narrower scope could truthfully
    /// be derived, so acting on that permission would be arbitrary rather than restrictive.
    /// </summary>
    private static void Require(ICurrentActor actor)
    {
        if (!actor.IsAuthenticated || !actor.HasPermission(Permissions.CandidatesRead))
        {
            throw new ForbiddenException();
        }
    }
}
