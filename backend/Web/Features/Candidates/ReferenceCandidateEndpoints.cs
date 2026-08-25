using KeplerTalento.Application.Features.Candidates;
using MediatR;
using KeplerTalento.Application.Abstractions.Documents;
using KeplerTalento.Application.Abstractions.Identity;
using KeplerTalento.Application.Abstractions.Operations;
using KeplerTalento.Domain.Operations;
using System.Text.RegularExpressions;

namespace KeplerTalento.Web.Features.Candidates;

public static class ReferenceCandidateEndpoints
{
    private const int MaximumCorrelationIdLength = 100;

    public sealed record ReferenceOperationResponse(
        Guid Id,
        string Status,
        string Type,
        string CorrelationId,
        int AttemptCount,
        string? OutcomeCode,
        DateTimeOffset CreatedAtUtc,
        DateTimeOffset UpdatedAtUtc);

    public static IEndpointRouteBuilder MapReferenceCandidateEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/reference/candidates/{id:guid}", async (
                Guid id,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var response = await sender.Send(new GetReferenceCandidateQuery(id), cancellationToken);
                return Results.Ok(response);
            })
            .WithName("GetReferenceCandidate")
            .WithTags("Reference")
            .Produces<ReferenceCandidateResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError);
        endpoints.MapGet("/api/reference/documents/{id:guid}", async (
                Guid id,
                IDocumentDownloadService downloads,
                ICurrentActor actor,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                if (!actor.IsAuthenticated || !actor.HasPermission(Permissions.CandidatesRead)) return Results.Forbid();
                var document = await downloads.OpenCleanAsync(id, cancellationToken);
                if (document is null) return Results.NotFound();
                context.Response.Headers.CacheControl = "private, no-store";
                return Results.File(document.Content, document.ContentType, document.FileName, enableRangeProcessing: false);
            })
            .WithName("DownloadReferenceDocument")
            .WithTags("Reference")
            .Produces(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);
        endpoints.MapGet("/api/reference/operations/{id:guid}", async (
                Guid id,
                IOperationRepository operations,
                ICurrentActor actor,
                CancellationToken cancellationToken) =>
            {
                if (!actor.IsAuthenticated || !actor.HasPermission(Permissions.CandidatesRead)) return Results.Forbid();
                var operation = await operations.FindAsync(id, cancellationToken);
                return operation is null ? Results.NotFound() : Results.Ok(ToResponse(operation));
            })
            .WithName("GetReferenceOperation")
            .WithTags("Reference")
            .Produces(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);
        endpoints.MapGet("/api/reference/operations", async (
                string correlationId,
                IOperationRepository operations,
                ICurrentActor actor,
                CancellationToken cancellationToken) =>
            {
                if (!actor.IsAuthenticated || !actor.HasPermission(Permissions.CandidatesRead)) return Results.Forbid();
                if (!IsValidCorrelationId(correlationId))
                {
                    return Results.Problem(
                        statusCode: StatusCodes.Status400BadRequest,
                        title: "Identificador de correlaciÃ³n no vÃ¡lido.",
                        extensions: new Dictionary<string, object?> { ["code"] = "operation.correlation.invalid" });
                }
                var operationsForCorrelation = await operations.FindByCorrelationIdAsync(correlationId, cancellationToken);
                return Results.Ok(operationsForCorrelation.Select(ToResponse));
            })
            .WithName("GetReferenceOperationsByCorrelation")
            .WithTags("Reference")
            .Produces<IReadOnlyList<ReferenceOperationResponse>>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden);
        return endpoints;
    }

    private static ReferenceOperationResponse ToResponse(Operation operation) => new(
        operation.Id,
        operation.Status.ToString().ToLowerInvariant(),
        operation.Type,
        operation.CorrelationId,
        operation.AttemptCount,
        operation.OutcomeCode,
        operation.CreatedAtUtc,
        operation.UpdatedAtUtc);

    private static bool IsValidCorrelationId(string value) =>
        !string.IsNullOrWhiteSpace(value) &&
        value.Length <= MaximumCorrelationIdLength &&
        Regex.IsMatch(value, "^[A-Za-z0-9._:-]+$", RegexOptions.CultureInvariant);
}
