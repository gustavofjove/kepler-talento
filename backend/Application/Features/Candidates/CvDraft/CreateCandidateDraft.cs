using KeplerTalento.Application.Abstractions.Correlation;
using KeplerTalento.Application.Abstractions.CvExtraction;
using KeplerTalento.Application.Abstractions.Documents;
using KeplerTalento.Application.Abstractions.Identity;
using KeplerTalento.Application.Abstractions.Persistence;
using KeplerTalento.Application.Common.Errors;
using KeplerTalento.Domain.Auditing;
using KeplerTalento.Domain.Candidates;
using MediatR;

namespace KeplerTalento.Application.Features.Candidates.CvDraft;

/// <summary>
/// Extracts a suggested candidate draft from an uploaded CV (KTL-32).
/// </summary>
/// <param name="DraftId">Opaque id chosen by the endpoint so its log line and the audit agree.</param>
/// <param name="Content">A seekable, in-memory copy of the upload. Never stored.</param>
public sealed record CreateCandidateDraftCommand(Guid DraftId, string OriginalFileName, Stream Content)
    : IRequest<CandidateDraftResponse>;

public sealed record CandidateDraftFieldResponse(string Value, string Confidence);

/// <summary>
/// The draft: suggested values only. <see cref="Fields"/> holds a key for each form field with a
/// suggestion (<c>firstName</c>, <c>lastName</c>, <c>email</c>, <c>phone</c>, <c>location</c>,
/// <c>province</c>) and omits the others.
/// </summary>
public sealed record CandidateDraftResponse(
    Guid DraftId,
    string Outcome,
    IReadOnlyDictionary<string, CandidateDraftFieldResponse> Fields);

public static class CvDraftCodes
{
    public const string Extracted = "cv_draft.extracted";
    public const string NoText = "cv_draft.no_text";
    public const string FileMissing = "cv_draft.file.missing";
    public const string FileEmpty = "cv_draft.file.empty";
    public const string SizeExceeded = "cv_draft.size.exceeded";
    public const string FormatUnsupported = "cv_draft.format.unsupported";
    public const string ContentRejected = "cv_draft.content.rejected";
    public const string Rejected = "cv_draft.rejected";
    public const string Unreadable = "cv_draft.unreadable";
    public const string TooComplex = "cv_draft.too_complex";
    public const string ScannerUnavailable = "cv_draft.scanner_unavailable";
    public const string Busy = "cv_draft.busy";
    public const string Failed = "cv_draft.failed";

    public const string FileMissingMessage = "Debe seleccionar un archivo.";
    public const string FileEmptyMessage = "El archivo está vacío.";
    public const string SizeExceededMessage = "El archivo supera el máximo permitido de 20 MB.";
    public const string FormatUnsupportedMessage = "Solo se pueden leer CV en PDF o DOCX.";
    public const string ContentRejectedMessage = "El tipo o el contenido del archivo no está permitido.";
    public const string NotProcessableMessage = "No se ha podido procesar el CV.";
    public const string TooComplexMessage = "El CV es demasiado extenso para leerlo automáticamente.";
    public const string ScannerUnavailableMessage =
        "No se puede analizar el archivo en este momento. Inténtelo de nuevo más tarde.";
    public const string BusyMessage = "Hay demasiados CV en proceso. Inténtelo de nuevo en unos segundos.";

    public static RequestValidationException Validation(string code, string message) =>
        new([new ValidationIssue("File", code, message)]);

    /// <summary>The code an attempt ended with, for the audit trail.</summary>
    public static string OutcomeOf(Exception exception) => exception switch
    {
        RequestValidationException validation => validation.Issues.FirstOrDefault()?.Code ?? validation.Code,
        ApplicationExceptionBase known => known.Code,
        _ => Failed,
    };
}

/// <remarks>
/// <para>
/// Order matters and is the security property of the slice: permission, then format and content
/// checks, then the malware scan, and only on a clean verdict does any parser see the bytes
/// (design D1, D2). Nothing here writes the file, its name, its text or a suggested value
/// anywhere: not storage, not the database, not a log, not the audit trail.
/// </para>
/// <para>
/// Every attempt that passes the permission check records one audit event whose subject is the
/// draft id and whose outcome is a code (design D8).
/// </para>
/// </remarks>
public sealed class CreateCandidateDraftHandler(
    IDocumentContentInspector inspector,
    IMalwareScanner scanner,
    ICvTextReader reader,
    ICandidateDraftExtractor extractor,
    CvDraftOptions options,
    IAuditRepository audits,
    ICorrelationContext correlation,
    ICurrentActor actor) : IRequestHandler<CreateCandidateDraftCommand, CandidateDraftResponse>
{
    public async Task<CandidateDraftResponse> Handle(
        CreateCandidateDraftCommand request,
        CancellationToken cancellationToken)
    {
        CandidateGuards.RequireCreate(actor);
        var auditActor = actor.ToAuditActor();

        var outcome = CvDraftCodes.Failed;
        try
        {
            var response = await ExtractAsync(request, cancellationToken);
            outcome = response.Outcome;
            return response;
        }
        catch (Exception exception)
        {
            outcome = CvDraftCodes.OutcomeOf(exception);
            throw;
        }
        finally
        {
            // Recorded even when the caller has gone away, so the attempt is still accounted for.
            await audits.RecordAsync(
                new AuditEvent(
                    Guid.CreateVersion7(),
                    CandidateAuditEvents.CvDraftExtracted,
                    request.DraftId.ToString("N"),
                    correlation.CorrelationId,
                    DateTimeOffset.UtcNow,
                    auditActor,
                    outcome),
                CancellationToken.None);
        }
    }

    private async Task<CandidateDraftResponse> ExtractAsync(
        CreateCandidateDraftCommand request,
        CancellationToken cancellationToken)
    {
        var kind = KindOf(request.OriginalFileName)
            ?? throw CvDraftCodes.Validation(CvDraftCodes.FormatUnsupported, CvDraftCodes.FormatUnsupportedMessage);
        var content = request.Content;
        if (!content.CanSeek || content.Length == 0)
        {
            throw CvDraftCodes.Validation(CvDraftCodes.FileEmpty, CvDraftCodes.FileEmptyMessage);
        }

        // Magic bytes and container structure only; this reads no text.
        content.Position = 0;
        var inspection = await inspector.InspectAsync(request.OriginalFileName, content, cancellationToken);
        if (!inspection.Accepted)
        {
            throw CvDraftCodes.Validation(CvDraftCodes.ContentRejected, CvDraftCodes.ContentRejectedMessage);
        }

        content.Position = 0;
        var scan = await scanner.ScanAsync(content, cancellationToken);
        switch (scan.Verdict)
        {
            case ScanVerdict.Clean:
                break;
            case ScanVerdict.Infected:
                // Deliberately the same message as unreadable content: no signature, no "virus".
                throw new UnprocessableException(CvDraftCodes.Rejected, CvDraftCodes.NotProcessableMessage);
            default:
                throw new ServiceUnavailableException(
                    CvDraftCodes.ScannerUnavailable,
                    CvDraftCodes.ScannerUnavailableMessage);
        }

        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budget.CancelAfter(TimeSpan.FromSeconds(options.TimeBudgetSeconds));
        CvText text;
        try
        {
            content.Position = 0;
            text = await reader.ReadAsync(kind, content, options.Bounds, budget.Token);
        }
        catch (CvUnreadableException)
        {
            throw new UnprocessableException(CvDraftCodes.Unreadable, CvDraftCodes.NotProcessableMessage);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new UnprocessableException(CvDraftCodes.TooComplex, CvDraftCodes.TooComplexMessage);
        }

        if (!text.HasText)
        {
            return new(request.DraftId, CvDraftCodes.NoText, new Dictionary<string, CandidateDraftFieldResponse>());
        }
        var suggestions = extractor.Extract(text);
        return new(request.DraftId, CvDraftCodes.Extracted, ToFields(suggestions));
    }

    private static CvFileKind? KindOf(string fileName) =>
        Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".pdf" => CvFileKind.Pdf,
            ".docx" => CvFileKind.Docx,
            _ => null,
        };

    private static Dictionary<string, CandidateDraftFieldResponse> ToFields(CandidateDraftSuggestions suggestions)
    {
        var fields = new Dictionary<string, CandidateDraftFieldResponse>(StringComparer.Ordinal);
        Add(fields, "firstName", suggestions.FirstName);
        Add(fields, "lastName", suggestions.LastName);
        Add(fields, "email", suggestions.Email);
        Add(fields, "phone", suggestions.Phone);
        Add(fields, "location", suggestions.Location);
        Add(fields, "province", suggestions.Province);
        return fields;
    }

    private static void Add(
        Dictionary<string, CandidateDraftFieldResponse> fields,
        string key,
        FieldSuggestion? suggestion)
    {
        if (suggestion is null || string.IsNullOrWhiteSpace(suggestion.Value))
        {
            return;
        }
        fields[key] = new(
            suggestion.Value.Trim(),
            suggestion.Confidence == SuggestionConfidence.High ? "high" : "low");
    }
}
