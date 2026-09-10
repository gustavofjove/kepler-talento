using KeplerTalento.Application.Abstractions.Documents;
using KeplerTalento.Application.Abstractions.Identity;
using KeplerTalento.Application.Common.Errors;
using KeplerTalento.Application.Features.Documents;
using KeplerTalento.Infrastructure.Documents;
using MediatR;

namespace KeplerTalento.Web.Features.Documents;

public static class DocumentEndpoints
{
    public static IEndpointRouteBuilder MapDocumentEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/candidates/{candidateId:guid}/documents").WithTags("Documents");

        group.MapPost("/", UploadAsync)
            .DisableAntiforgery()
            .WithName("UploadCandidateDocument")
            .Accepts<IFormFile>("multipart/form-data")
            .Produces<CandidateDocumentResponse>(StatusCodes.Status202Accepted)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/", async (Guid candidateId, ISender sender, ICurrentActor actor, CancellationToken cancellationToken) =>
            {
                Require(actor, Permissions.CandidatesRead);
                return Results.Ok(await sender.Send(new ListCandidateDocumentsQuery(candidateId), cancellationToken));
            })
            .WithName("ListCandidateDocuments")
            .Produces<IReadOnlyList<CandidateDocumentResponse>>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/{documentId:guid}", async (Guid candidateId, Guid documentId, ISender sender, ICurrentActor actor, CancellationToken cancellationToken) =>
            {
                Require(actor, Permissions.CandidatesRead);
                return Results.Ok(await sender.Send(new GetCandidateDocumentQuery(candidateId, documentId), cancellationToken));
            })
            .WithName("GetCandidateDocument")
            .Produces<CandidateDocumentResponse>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPut("/{documentId:guid}/primary", async (Guid candidateId, Guid documentId, ISender sender, ICurrentActor actor, CancellationToken cancellationToken) =>
            {
                Require(actor, Permissions.DocumentsUpload);
                return Results.Ok(await sender.Send(new SetPrimaryCandidateDocumentCommand(candidateId, documentId), cancellationToken));
            })
            .WithName("SetPrimaryCandidateDocument")
            .Produces<CandidateDocumentResponse>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapDelete("/{documentId:guid}", async (Guid candidateId, Guid documentId, ISender sender, ICurrentActor actor, CancellationToken cancellationToken) =>
            {
                Require(actor, Permissions.DocumentsUpload);
                await sender.Send(new RemoveCandidateDocumentCommand(candidateId, documentId), cancellationToken);
                return Results.NoContent();
            })
            .WithName("RemoveCandidateDocument")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/{documentId:guid}/content", DownloadAsync)
            .WithName("DownloadCandidateDocument")
            .Produces(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return endpoints;
    }

    private static async Task<IResult> UploadAsync(
        Guid candidateId,
        HttpRequest request,
        ISender sender,
        ICurrentActor actor,
        DocumentStorageOptions options,
        CancellationToken cancellationToken)
    {
        // Authorization deliberately precedes ReadFormAsync so an unauthorized request's
        // externally supplied bytes are never parsed or written.
        Require(actor, Permissions.DocumentsUpload);
        if (request.ContentLength > options.MaximumRequestBodyBytes)
        {
            throw DocumentErrors.Validation("document.size.exceeded", "El archivo supera el máximo permitido de 20 MB.");
        }
        if (!request.HasFormContentType)
        {
            throw DocumentErrors.Validation(DocumentErrors.MissingFile, "Debe seleccionar un archivo.");
        }
        IFormCollection form;
        try
        {
            form = await request.ReadFormAsync(cancellationToken);
        }
        catch (InvalidDataException)
        {
            throw DocumentErrors.Validation(DocumentErrors.MissingFile, "Debe seleccionar un archivo.");
        }
        var file = form.Files.GetFile("file");
        if (file is null)
        {
            throw DocumentErrors.Validation(DocumentErrors.MissingFile, "Debe seleccionar un archivo.");
        }
        if (file.Length > options.MaximumBytes)
        {
            throw DocumentErrors.Validation("document.size.exceeded", "El archivo supera el máximo permitido de 20 MB.");
        }
        var documentType = form["documentType"].FirstOrDefault() ?? "CV";
        var isPrimary = bool.TryParse(form["isPrimary"].FirstOrDefault(), out var parsed) && parsed;
        await using var content = file.OpenReadStream();
        var response = await sender.Send(
            new UploadCandidateDocumentCommand(
                candidateId,
                file.FileName,
                documentType,
                isPrimary,
                content,
                options.MaximumBytes),
            cancellationToken);
        return Results.Accepted($"/api/candidates/{candidateId}/documents/{response.Id}", response);
    }

    private static async Task<IResult> DownloadAsync(
        Guid candidateId,
        Guid documentId,
        HttpContext context,
        ISender sender,
        ICurrentActor actor,
        CancellationToken cancellationToken)
    {
        // A missing capability and a missing identifier have the same public shape.
        if (!actor.IsAuthenticated || !actor.HasPermission(Permissions.DocumentsDownload))
        {
            return Results.NotFound();
        }
        var download = await sender.Send(new DownloadCandidateDocumentQuery(candidateId, documentId), cancellationToken);
        context.Response.Headers.CacheControl = "private, no-store";
        context.Response.Headers.XContentTypeOptions = "nosniff";
        return Results.File(
            download.Content,
            download.ContentType,
            download.FileName,
            enableRangeProcessing: false);
    }

    private static void Require(ICurrentActor actor, string permission)
    {
        if (!actor.IsAuthenticated || !actor.HasPermission(permission)) throw new ForbiddenException();
    }
}
