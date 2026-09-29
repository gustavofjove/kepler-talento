using System.Diagnostics;
using KeplerTalento.Application.Abstractions.Identity;
using KeplerTalento.Application.Common.Errors;
using KeplerTalento.Application.Features.Candidates.CvDraft;
using KeplerTalento.Infrastructure.Documents;
using MediatR;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Net.Http.Headers;

namespace KeplerTalento.Web.Features.Candidates;

/// <summary>
/// Suggested values for the create form from an uploaded CV (KTL-32). Nothing is stored.
/// </summary>
public static class CandidateDraftEndpoints
{
    private const string FilePartName = "file";
    private const int CopyBufferBytes = 64 * 1024;

    public static IEndpointRouteBuilder MapCandidateDraftEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/candidates").WithTags("Candidates");

        group.MapPost("/draft-from-document", CreateAsync)
            .DisableAntiforgery()
            .WithName("CreateCandidateDraftFromDocument")
            // No .Accepts<>: routing would answer a wrong content type with 415 before the
            // permission check, telling an unauthorized caller the route's shape (see KTL-17).
            .Produces<CandidateDraftResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        return endpoints;
    }

    private static async Task<IResult> CreateAsync(
        HttpContext context,
        ISender sender,
        ICurrentActor actor,
        DocumentStorageOptions storage,
        CvDraftGate gate,
        ILoggerFactory loggers,
        CancellationToken cancellationToken)
    {
        // Authorization precedes every read of the body, so an unauthorized caller's bytes are
        // never buffered, scanned or parsed.
        if (!actor.IsAuthenticated || !actor.HasPermission(Permissions.CandidatesCreate))
        {
            throw new ForbiddenException();
        }
        if (!gate.TryEnter())
        {
            throw new TooManyRequestsException(CvDraftCodes.Busy, CvDraftCodes.BusyMessage);
        }
        try
        {
            var logger = loggers.CreateLogger(typeof(CandidateDraftEndpoints));
            var draftId = Guid.CreateVersion7();
            var started = Stopwatch.GetTimestamp();
            var outcome = CvDraftCodes.Failed;
            try
            {
                var (fileName, content) = await ReadFileAsync(context.Request, storage, cancellationToken);
                await using (content)
                {
                    var response = await sender.Send(
                        new CreateCandidateDraftCommand(draftId, fileName, content),
                        cancellationToken);
                    outcome = response.Outcome;
                    context.Response.Headers.CacheControl = "no-store";
                    return Results.Ok(response);
                }
            }
            catch (Exception exception)
            {
                outcome = CvDraftCodes.OutcomeOf(exception);
                throw;
            }
            finally
            {
                // Identifiers and a code only: never the filename, the text or a suggestion.
                logger.LogInformation(
                    "CV draft {DraftId} finished with {OutcomeCode} in {ElapsedMs} ms",
                    draftId,
                    outcome,
                    (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            }
        }
        finally
        {
            gate.Exit();
        }
    }

    /// <summary>
    /// Copies the <c>file</c> part into a capped in-memory buffer. <see cref="MultipartReader"/>
    /// is used instead of <c>ReadFormAsync</c>, which spools large files to a temp file: the CV
    /// must never reach the disk (design D2).
    /// </summary>
    private static async Task<(string FileName, MemoryStream Content)> ReadFileAsync(
        HttpRequest request,
        DocumentStorageOptions storage,
        CancellationToken cancellationToken)
    {
        if (request.ContentLength > storage.MaximumRequestBodyBytes)
        {
            throw CvDraftCodes.Validation(CvDraftCodes.SizeExceeded, CvDraftCodes.SizeExceededMessage);
        }
        if (!MediaTypeHeaderValue.TryParse(request.ContentType, out var mediaType)
            || !mediaType.MediaType.Equals("multipart/form-data", StringComparison.OrdinalIgnoreCase)
            || HeaderUtilities.RemoveQuotes(mediaType.Boundary).Value is not { Length: > 0 } boundary)
        {
            throw CvDraftCodes.Validation(CvDraftCodes.FileMissing, CvDraftCodes.FileMissingMessage);
        }

        var reader = new MultipartReader(boundary, request.Body)
        {
            BodyLengthLimit = storage.MaximumRequestBodyBytes,
        };
        try
        {
            while (await reader.ReadNextSectionAsync(cancellationToken) is { } section)
            {
                if (!ContentDispositionHeaderValue.TryParse(section.ContentDisposition, out var disposition)
                    || !disposition.IsFileDisposition()
                    || !string.Equals(HeaderUtilities.RemoveQuotes(disposition.Name).Value, FilePartName, StringComparison.Ordinal))
                {
                    continue;
                }
                var fileName = Path.GetFileName(
                    HeaderUtilities.RemoveQuotes(disposition.FileNameStar).Value
                    ?? HeaderUtilities.RemoveQuotes(disposition.FileName).Value
                    ?? string.Empty);
                var content = await CopyCappedAsync(section.Body, storage.MaximumBytes, cancellationToken);
                if (content.Length == 0)
                {
                    await content.DisposeAsync();
                    throw CvDraftCodes.Validation(CvDraftCodes.FileEmpty, CvDraftCodes.FileEmptyMessage);
                }
                return (fileName, content);
            }
        }
        catch (InvalidDataException)
        {
            // Malformed multipart, or the body is longer than the limit.
            throw CvDraftCodes.Validation(CvDraftCodes.FileMissing, CvDraftCodes.FileMissingMessage);
        }
        throw CvDraftCodes.Validation(CvDraftCodes.FileMissing, CvDraftCodes.FileMissingMessage);
    }

    private static async Task<MemoryStream> CopyCappedAsync(Stream source, long maximumBytes, CancellationToken cancellationToken)
    {
        var content = new MemoryStream();
        var buffer = new byte[CopyBufferBytes];
        int read;
        while ((read = await source.ReadAsync(buffer, cancellationToken)) > 0)
        {
            if (content.Length + read > maximumBytes)
            {
                await content.DisposeAsync();
                throw CvDraftCodes.Validation(CvDraftCodes.SizeExceeded, CvDraftCodes.SizeExceededMessage);
            }
            await content.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }
        content.Position = 0;
        return content;
    }
}
