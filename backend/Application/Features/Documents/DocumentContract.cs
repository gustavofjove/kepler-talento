using KeplerTalento.Application.Abstractions.Documents;
using KeplerTalento.Application.Abstractions.Identity;
using KeplerTalento.Application.Common.Errors;
using KeplerTalento.Domain.Documents;

namespace KeplerTalento.Application.Features.Documents;

public sealed record CandidateDocumentResponse(
    Guid Id,
    Guid CandidateId,
    string DocumentType,
    string OriginalFilename,
    string MimeType,
    long SizeBytes,
    bool IsPrimary,
    DateTimeOffset UploadedAt,
    string ScanState,
    string AvailabilityState,
    string? FailureCode)
{
    public static CandidateDocumentResponse From(CandidateDocument document)
    {
        var legacyWithoutBinary = document.SourceKey is not null && string.IsNullOrWhiteSpace(document.Sha256);
        var availability = legacyWithoutBinary
            ? "LegacyUnavailable"
            : document.ScanState switch
            {
                DocumentScanState.PendingScan => "Pending",
                DocumentScanState.Clean => "Available",
                DocumentScanState.ScanFailed => "Error",
                _ => "Refused",
            };
        return new(
            document.Id,
            document.CandidateId,
            document.DocumentType,
            document.OriginalFileName,
            document.ContentType,
            document.Size,
            document.IsPrimary,
            document.CreatedAtUtc,
            document.ScanState.ToString(),
            availability,
            legacyWithoutBinary ? "document.legacy.binary_missing" : document.ScanFailureCode);
    }
}

public static class DocumentErrors
{
    public const string MissingFile = "document.file.required";
    public const string NotFound = "document.not_found";
    public const string PrimaryConflict = "document.primary.conflict";
    public const string NotAvailable = "document.not_available";

    public static RequestValidationException Validation(string code, string message) =>
        new([new ValidationIssue("File", code, message)]);

    public static NotFoundException Missing() => new(NotFound, "Documento no encontrado.");

    public static ConflictException PrimaryConflictException() =>
        new(PrimaryConflict, "Otro documento ya se ha marcado como principal.");

    public static void Require(ICurrentActor actor, string permission)
    {
        if (!actor.IsAuthenticated || !actor.HasPermission(permission)) throw new ForbiddenException();
    }
}
