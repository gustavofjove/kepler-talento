using KeplerTalento.Application.Abstractions.Identity;
using KeplerTalento.Application.Common.Errors;
using KeplerTalento.Application.Features.Candidates;
using MediatR;

namespace KeplerTalento.Web.Features.Candidates;

/// <summary>
/// The candidate aggregate. There is deliberately no DELETE verb anywhere in this group:
/// a candidate is removed logically, through the <c>active</c> sub-resource, and the
/// runtime database role holds no DELETE on the candidate tables either.
/// </summary>
/// <remarks>
/// Every route authorizes before dispatching. Doing it inside the handler would let an
/// unauthorized caller distinguish an existing candidate from a missing one by the shape
/// of the refusal, which is a disclosure the refusal exists to prevent.
/// </remarks>
public static class CandidateEndpoints
{
    public sealed record CandidateFieldsRequest(
        string FirstName,
        string LastName,
        string Phone,
        string Email,
        string Location,
        string Province,
        string Country,
        string Availability,
        string Status,
        string Source,
        string Notes,
        string? ReceivedAt,
        string? ConsentAt,
        string? ReviewDueAt);

    public sealed record UpdateCandidateRequest(
        string FirstName,
        string LastName,
        string Phone,
        string Email,
        string Location,
        string Province,
        string Country,
        string Availability,
        string Status,
        string Source,
        string Notes,
        string? ReceivedAt,
        string? ConsentAt,
        string? ReviewDueAt,
        uint Version);

    public sealed record SetCandidateActiveRequest(bool IsActive, uint Version);

    public sealed record SetLanguagesRequest(IReadOnlyList<CandidateLanguageInput>? Languages, uint Version);

    public sealed record SetProgramsRequest(IReadOnlyList<CandidateProgramInput>? Programs, uint Version);

    public sealed record SetEducationRequest(IReadOnlyList<CandidateEducationInput>? Education, uint Version);

    public sealed record SetExperienceRequest(IReadOnlyList<CandidateExperienceInput>? Experience, uint Version);

    public sealed record SetSkillsRequest(IReadOnlyList<CandidateSkillInput>? Skills, uint Version);

    public sealed record SetDocumentsRequest(IReadOnlyList<CandidateDocumentInput>? Documents, uint Version);

    public static IEndpointRouteBuilder MapCandidateEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/candidates").WithTags("Candidates");

        group.MapGet("/", async (
                bool? includeInactive,
                ISender sender,
                ICurrentActor actor,
                CancellationToken cancellationToken) =>
            {
                Require(actor, Permissions.CandidatesRead);
                var candidates = await sender.Send(
                    new ListCandidatesQuery(includeInactive ?? false),
                    cancellationToken);
                return Results.Ok(candidates);
            })
            .WithName("ListCandidates")
            .Produces<IReadOnlyList<CandidateSummaryResponse>>()
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapGet("/{id:guid}", async (
                Guid id,
                ISender sender,
                ICurrentActor actor,
                CancellationToken cancellationToken) =>
            {
                Require(actor, Permissions.CandidatesRead);
                var candidate = await sender.Send(new GetCandidateQuery(id), cancellationToken);
                return Results.Ok(candidate);
            })
            .WithName("GetCandidate")
            .Produces<CandidateResponse>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/", async (
                CandidateFieldsRequest request,
                ISender sender,
                ICurrentActor actor,
                CancellationToken cancellationToken) =>
            {
                Require(actor, Permissions.CandidatesCreate);
                var created = await sender.Send(
                    new CreateCandidateCommand(
                        request.FirstName,
                        request.LastName,
                        request.Phone,
                        request.Email,
                        request.Location,
                        request.Province,
                        request.Country,
                        request.Availability,
                        request.Status,
                        request.Source,
                        request.Notes,
                        request.ReceivedAt,
                        request.ConsentAt,
                        request.ReviewDueAt),
                    cancellationToken);
                return Results.Created($"/api/candidates/{created.Id}", created);
            })
            .WithName("CreateCandidate")
            .Produces<CandidateResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapPut("/{id:guid}", async (
                Guid id,
                UpdateCandidateRequest request,
                ISender sender,
                ICurrentActor actor,
                CancellationToken cancellationToken) =>
            {
                Require(actor, Permissions.CandidatesUpdate);
                var updated = await sender.Send(
                    new UpdateCandidateCommand(
                        id,
                        request.FirstName,
                        request.LastName,
                        request.Phone,
                        request.Email,
                        request.Location,
                        request.Province,
                        request.Country,
                        request.Availability,
                        request.Status,
                        request.Source,
                        request.Notes,
                        request.ReceivedAt,
                        request.ConsentAt,
                        request.ReviewDueAt,
                        request.Version),
                    cancellationToken);
                return Results.Ok(updated);
            })
            .WithName("UpdateCandidate")
            .Produces<CandidateResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        // Removal and restoration are the same transition, and candidates.delete governs
        // both: restoring a withdrawn record is as consequential as withdrawing it.
        group.MapPut("/{id:guid}/active", async (
                Guid id,
                SetCandidateActiveRequest request,
                ISender sender,
                ICurrentActor actor,
                CancellationToken cancellationToken) =>
            {
                Require(actor, Permissions.CandidatesDelete);
                var updated = await sender.Send(
                    new SetCandidateActiveCommand(id, request.IsActive, request.Version),
                    cancellationToken);
                return Results.Ok(updated);
            })
            .WithName("SetCandidateActive")
            .Produces<CandidateResponse>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        // The six collection routes share a shape — PUT the complete set with the
        // candidate's version — but each is mapped with its own concrete request type.
        // A generic helper cannot stand in for them: minimal APIs do not infer a body
        // binding for a generic type parameter, and the request silently arrives with
        // default values instead.
        group.MapPut("/{id:guid}/languages", async (
                Guid id,
                SetLanguagesRequest request,
                ISender sender,
                ICurrentActor actor,
                CancellationToken cancellationToken) =>
            {
                Require(actor, Permissions.CandidatesUpdate);
                return Results.Ok(await sender.Send(
                    new SetCandidateLanguagesCommand(id, request.Languages ?? [], request.Version),
                    cancellationToken));
            })
            .WithName("SetCandidateLanguages")
            .WithCollectionResponses();

        group.MapPut("/{id:guid}/programs", async (
                Guid id,
                SetProgramsRequest request,
                ISender sender,
                ICurrentActor actor,
                CancellationToken cancellationToken) =>
            {
                Require(actor, Permissions.CandidatesUpdate);
                return Results.Ok(await sender.Send(
                    new SetCandidateProgramsCommand(id, request.Programs ?? [], request.Version),
                    cancellationToken));
            })
            .WithName("SetCandidatePrograms")
            .WithCollectionResponses();

        group.MapPut("/{id:guid}/education", async (
                Guid id,
                SetEducationRequest request,
                ISender sender,
                ICurrentActor actor,
                CancellationToken cancellationToken) =>
            {
                Require(actor, Permissions.CandidatesUpdate);
                return Results.Ok(await sender.Send(
                    new SetCandidateEducationCommand(id, request.Education ?? [], request.Version),
                    cancellationToken));
            })
            .WithName("SetCandidateEducation")
            .WithCollectionResponses();

        group.MapPut("/{id:guid}/experience", async (
                Guid id,
                SetExperienceRequest request,
                ISender sender,
                ICurrentActor actor,
                CancellationToken cancellationToken) =>
            {
                Require(actor, Permissions.CandidatesUpdate);
                return Results.Ok(await sender.Send(
                    new SetCandidateExperienceCommand(id, request.Experience ?? [], request.Version),
                    cancellationToken));
            })
            .WithName("SetCandidateExperience")
            .WithCollectionResponses();

        group.MapPut("/{id:guid}/skills", async (
                Guid id,
                SetSkillsRequest request,
                ISender sender,
                ICurrentActor actor,
                CancellationToken cancellationToken) =>
            {
                Require(actor, Permissions.CandidatesUpdate);
                return Results.Ok(await sender.Send(
                    new SetCandidateSkillsCommand(id, request.Skills ?? [], request.Version),
                    cancellationToken));
            })
            .WithName("SetCandidateSkills")
            .WithCollectionResponses();

        group.MapPut("/{id:guid}/documents", async (
                Guid id,
                SetDocumentsRequest request,
                ISender sender,
                ICurrentActor actor,
                CancellationToken cancellationToken) =>
            {
                Require(actor, Permissions.CandidatesUpdate);
                return Results.Ok(await sender.Send(
                    new SetCandidateDocumentsCommand(id, request.Documents ?? [], request.Version),
                    cancellationToken));
            })
            .WithName("SetCandidateDocuments")
            .WithCollectionResponses();

        return endpoints;
    }

    private static RouteHandlerBuilder WithCollectionResponses(this RouteHandlerBuilder builder) =>
        builder
            .Produces<CandidateResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

    private static void Require(ICurrentActor actor, string permission)
    {
        if (!actor.IsAuthenticated || !actor.HasPermission(permission))
        {
            throw new ForbiddenException();
        }
    }
}
