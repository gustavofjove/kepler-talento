using KeplerTalento.Application.Abstractions.Identity;
using KeplerTalento.Application.Abstractions.Import;
using KeplerTalento.Application.Features.Import;
using KeplerTalento.Infrastructure.Documents;
using MediatR;

namespace KeplerTalento.Web.Features.Import;

/// <summary>
/// Candidate import: upload, validate as a dry run, commit, and read the history and row reports.
/// </summary>
/// <remarks>
/// <para>
/// The group requires the <c>candidates.import</c> policy, which runs before minimal-API body
/// binding and before the multipart form is read. That is what makes a malformed request from a
/// caller without the permission indistinguishable from a well-formed one: both get the forbidden
/// refusal, and neither gets a validation problem or has its file stored. Every handler repeats the
/// guard. Unauthenticated callers are refused by the fallback policy in <c>Program.cs</c>.
/// </para>
/// <para>
/// There is no DELETE verb. A batch is a record of work that happened; its file is purged on
/// schedule and the record stays.
/// </para>
/// </remarks>
public static class ImportEndpoints
{
    public sealed record TransitionRequest(uint Version);

    public static IEndpointRouteBuilder MapImportEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/import/batches")
            .WithTags("Import")
            .RequireAuthorization(Permissions.CandidatesImport);

        // Deliberately no .Accepts<IFormFile>("multipart/form-data"): that metadata makes endpoint
        // routing answer 415 for any other content type before authorization runs, which would tell
        // a caller without the permission something about the request shape. The handler reads the
        // multipart form itself, after the policy has admitted the caller.
        group.MapPost("/", UploadAsync)
            .DisableAntiforgery()
            .WithName("UploadImportFile")
            .WithDescription("multipart/form-data with a single `file` part holding a UTF-8 CSV.")
            .Produces<ImportBatchResponse>(StatusCodes.Status202Accepted)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapGet("/", async (int? page, int? pageSize, ISender sender, CancellationToken cancellationToken) =>
                Results.Ok(await sender.Send(new ListImportBatchesQuery(page ?? 1, pageSize ?? 20), cancellationToken)))
            .WithName("ListImportBatches")
            .Produces<ImportBatchPageResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapGet("/{batchId:guid}", async (Guid batchId, ISender sender, CancellationToken cancellationToken) =>
                Results.Ok(await sender.Send(new GetImportBatchQuery(batchId), cancellationToken)))
            .WithName("GetImportBatch")
            .Produces<ImportBatchResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{batchId:guid}/validation", async (
                Guid batchId,
                TransitionRequest request,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var batch = await sender.Send(new ValidateImportBatchCommand(batchId, request.Version), cancellationToken);
                return Results.Accepted($"/api/import/batches/{batch.Id}", batch);
            })
            .WithName("ValidateImportBatch")
            .Produces<ImportBatchResponse>(StatusCodes.Status202Accepted)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{batchId:guid}/commit", async (
                Guid batchId,
                TransitionRequest request,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var batch = await sender.Send(new CommitImportBatchCommand(batchId, request.Version), cancellationToken);
                return Results.Accepted($"/api/import/batches/{batch.Id}", batch);
            })
            .WithName("CommitImportBatch")
            .Produces<ImportBatchResponse>(StatusCodes.Status202Accepted)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapGet("/{batchId:guid}/rows", async (
                Guid batchId,
                string? phase,
                int? page,
                int? pageSize,
                ISender sender,
                CancellationToken cancellationToken) =>
                Results.Ok(await sender.Send(
                    new GetImportRowReportQuery(batchId, phase, page ?? 1, pageSize ?? 50),
                    cancellationToken)))
            .WithName("GetImportRowReport")
            .Produces<ImportRowReportResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return endpoints;
    }

    private static async Task<IResult> UploadAsync(
        HttpRequest request,
        ISender sender,
        IImportFileStorage importStorage,
        CancellationToken cancellationToken)
    {
        if (request.ContentLength > importStorage.MaximumBytes + DocumentStorageOptions.MultipartEnvelopeBytes)
        {
            throw ImportErrors.Validation(ImportErrors.FileTooLarge, "El archivo supera el tamaño máximo permitido.");
        }
        if (!request.HasFormContentType)
        {
            throw ImportErrors.Validation(ImportErrors.FileMissing, "Debe seleccionar un archivo.");
        }
        IFormCollection form;
        try
        {
            form = await request.ReadFormAsync(cancellationToken);
        }
        catch (InvalidDataException)
        {
            throw ImportErrors.Validation(ImportErrors.FileMissing, "Debe seleccionar un archivo.");
        }
        var file = form.Files.GetFile("file")
            ?? throw ImportErrors.Validation(ImportErrors.FileMissing, "Debe seleccionar un archivo.");
        if (file.Length > importStorage.MaximumBytes)
        {
            throw ImportErrors.Validation(ImportErrors.FileTooLarge, "El archivo supera el tamaño máximo permitido.");
        }
        await using var content = file.OpenReadStream();
        var batch = await sender.Send(new UploadImportFileCommand(file.FileName, content), cancellationToken);
        return Results.Accepted($"/api/import/batches/{batch.Id}", batch);
    }
}
