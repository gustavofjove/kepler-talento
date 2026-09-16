using System.Globalization;
using KeplerTalento.Application.Abstractions.Identity;
using KeplerTalento.Application.Common.Errors;
using KeplerTalento.Application.Features.Search;
using MediatR;

namespace KeplerTalento.Web.Features.Search;

/// <summary>
/// Candidate search and the shared saved-search library that drives it.
/// </summary>
/// <remarks>
/// Search is a POST even though it reads nothing but data. The filter value carries nested
/// criteria pairs that a query string expresses badly, and — the deciding reason — search
/// terms are personal data that would otherwise appear in every proxy and access log that
/// records a URL. POST does not make the operation stateful: the handler writes nothing.
///
/// Every route authorizes before dispatching, so an unauthorized caller cannot distinguish a
/// valid request from an invalid one, nor an existing preset from a missing one, by the shape
/// of the refusal. Reading and applying presets needs <c>candidates.read</c>; writing them
/// needs <c>presets.manage</c> (KTL-14).
/// </remarks>
public static class SearchEndpoints
{
    public sealed record SearchCandidatesRequest(SearchFiltersInput? Filters, int? Page, int? PageSize);

    public sealed record SearchPresetRequest(string? Name, SearchFiltersInput? Filters);

    /// <summary>
    /// The version is nullable so an omitted one reaches validation — after authorization —
    /// as an invalid version, rather than being quietly read as zero by the binder.
    /// </summary>
    public sealed record SearchPresetUpdateRequest(string? Name, SearchFiltersInput? Filters, uint? Version);

    public static IEndpointRouteBuilder MapSearchEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/candidates/search", async (
                SearchCandidatesRequest request,
                ISender sender,
                ICurrentActor actor,
                CancellationToken cancellationToken) =>
            {
                Require(actor, Permissions.CandidatesRead);
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
                Require(actor, Permissions.CandidatesRead);
                return Results.Ok(await sender.Send(new ListSearchPresetsQuery(), cancellationToken));
            })
            .WithName("ListSearchPresets")
            .Produces<IReadOnlyList<SearchPresetResponse>>()
            .ProducesProblem(StatusCodes.Status403Forbidden);

        presets.MapGet("/{id:guid}", async (
                Guid id,
                ISender sender,
                ICurrentActor actor,
                CancellationToken cancellationToken) =>
            {
                Require(actor, Permissions.CandidatesRead);
                return Results.Ok(await sender.Send(new GetSearchPresetQuery(id), cancellationToken));
            })
            .WithName("GetSearchPreset")
            .Produces<SearchPresetResponse>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        presets.MapPost("/", async (
                SearchPresetRequest request,
                ISender sender,
                ICurrentActor actor,
                CancellationToken cancellationToken) =>
            {
                Require(actor, Permissions.PresetsManage);
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
                SearchPresetUpdateRequest request,
                ISender sender,
                ICurrentActor actor,
                CancellationToken cancellationToken) =>
            {
                Require(actor, Permissions.PresetsManage);
                return Results.Ok(await sender.Send(
                    new UpdateSearchPresetCommand(id, request.Name, request.Filters, request.Version ?? 0),
                    cancellationToken));
            })
            .WithName("UpdateSearchPreset")
            .WithPresetResponses()
            .ProducesProblem(StatusCodes.Status409Conflict);

        // The version travels in the query string: a body on DELETE is poorly supported by
        // proxies and clients, and a version number is not personal data. It is bound as a
        // string on purpose — a typed parameter would let the binder reject "?version=abc"
        // with a 400 before this handler, and so before authorization, runs.
        presets.MapDelete("/{id:guid}", async (
                Guid id,
                string? version,
                ISender sender,
                ICurrentActor actor,
                CancellationToken cancellationToken) =>
            {
                Require(actor, Permissions.PresetsManage);
                var expected = uint.TryParse(version, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed)
                    ? parsed
                    : 0u;
                await sender.Send(new DeleteSearchPresetCommand(id, expected), cancellationToken);
                return Results.NoContent();
            })
            .WithName("DeleteSearchPreset")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        // Applying a preset changes its last-used time, so it is a POST on a sub-resource
        // rather than a GET that quietly writes.
        presets.MapPost("/{id:guid}/use", async (
                Guid id,
                ISender sender,
                ICurrentActor actor,
                CancellationToken cancellationToken) =>
            {
                Require(actor, Permissions.CandidatesRead);
                return Results.Ok(await sender.Send(new UseSearchPresetCommand(id), cancellationToken));
            })
            .WithName("UseSearchPreset")
            .WithPresetResponses()
            .ProducesProblem(StatusCodes.Status409Conflict);

        return endpoints;
    }

    private static RouteHandlerBuilder WithPresetResponses(this RouteHandlerBuilder builder) =>
        builder
            .Produces<SearchPresetResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

    /// <summary>
    /// There is deliberately no broader "see every candidate" permission for reads: see the
    /// KTL-10 design — the domain models no ownership or team from which a narrower scope could
    /// truthfully be derived, so acting on such a permission would be arbitrary rather than
    /// restrictive. KTL-16 removed the frontend's inert <c>view_all_candidates</c> accordingly.
    /// </summary>
    private static void Require(ICurrentActor actor, string permission)
    {
        if (!actor.IsAuthenticated || !actor.HasPermission(permission))
        {
            throw new ForbiddenException();
        }
    }
}
