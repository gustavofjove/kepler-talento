using FluentValidation;
using KeplerTalento.Application.Abstractions.Identity;
using KeplerTalento.Application.Abstractions.Persistence;
using KeplerTalento.Application.Common.Errors;
using KeplerTalento.Domain.Candidates;
using KeplerTalento.Domain.Documents;

namespace KeplerTalento.Application.Features.Candidates;

/// <summary>
/// The candidate as the list screen sees it: core fields, no collections.
/// </summary>
public sealed record CandidateSummaryResponse(
    Guid Id,
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
    string ReceivedAt,
    string ConsentAt,
    string ReviewDueAt,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    uint Version,
    int DocumentCount,
    Guid? PrimaryDocumentId)
{
    public static CandidateSummaryResponse From(CandidateSummary summary) => new(
        summary.Id,
        summary.FirstName,
        summary.LastName,
        summary.Phone,
        summary.Email,
        summary.Location,
        summary.Province,
        summary.Country,
        summary.Availability,
        summary.Status,
        summary.Source,
        summary.Notes,
        CandidateDates.ToWire(summary.ReceivedAt),
        CandidateDates.ToWire(summary.ConsentAt),
        CandidateDates.ToWire(summary.ReviewDueAt),
        summary.IsActive,
        summary.CreatedAtUtc,
        summary.UpdatedAtUtc,
        summary.Version,
        summary.DocumentCount,
        summary.PrimaryDocumentId);

    public static CandidateSummaryResponse From(
        Candidate candidate,
        int documentCount,
        Guid? primaryDocumentId) => new(
        candidate.Id,
        candidate.FirstName,
        candidate.LastName,
        candidate.Phone,
        candidate.Email,
        candidate.Location,
        candidate.Province,
        candidate.Country,
        candidate.Availability,
        candidate.Status,
        candidate.Source,
        candidate.Notes,
        CandidateDates.ToWire(candidate.ReceivedAt),
        CandidateDates.ToWire(candidate.ConsentAt),
        CandidateDates.ToWire(candidate.ReviewDueAt),
        candidate.IsActive,
        candidate.CreatedAtUtc,
        candidate.UpdatedAtUtc,
        candidate.Version,
        documentCount,
        primaryDocumentId);
}

public sealed record CandidateLanguageResponse(
    Guid Id,
    string Language,
    string Level,
    string? Certification,
    string? Notes);

public sealed record CandidateProgramResponse(
    Guid Id,
    string Program,
    string Level,
    int? YearsExperience,
    string? Notes);

public sealed record CandidateEducationResponse(
    Guid Id,
    string EducationType,
    string Degree,
    string? Specialty,
    string Institution,
    string Status,
    int? EndYear,
    string? Notes);

public sealed record CandidateExperienceResponse(
    Guid Id,
    string Company,
    string Position,
    string Sector,
    string? Functions,
    string? StartDate,
    string? EndDate,
    int? YearsExperience,
    bool IsCurrent,
    string? Notes);

public sealed record CandidateSkillResponse(
    Guid Id,
    string Skill,
    string Level,
    string? Notes);

/// <summary>
/// Document metadata as a caller sees it. There is deliberately no storage key, path or
/// scan-state field: the caller learns what the document is, never where it lives.
/// </summary>
public sealed record CandidateDocumentResponse(
    Guid Id,
    string DocumentType,
    string OriginalFilename,
    string MimeType,
    long SizeBytes,
    bool IsPrimary,
    DateTimeOffset UploadedAt)
{
    public static CandidateDocumentResponse From(CandidateDocument document) => new(
        document.Id,
        document.DocumentType,
        document.OriginalFileName,
        document.ContentType,
        document.Size,
        document.IsPrimary,
        document.CreatedAtUtc);
}

/// <summary>The complete aggregate, as the detail screen loads it.</summary>
public sealed record CandidateResponse(
    Guid Id,
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
    string ReceivedAt,
    string ConsentAt,
    string ReviewDueAt,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    uint Version,
    /// <summary>Derived from <c>Documents</c>; present so the aggregate is a superset of the summary.</summary>
    int DocumentCount,
    Guid? PrimaryDocumentId,
    IReadOnlyList<CandidateLanguageResponse> Languages,
    IReadOnlyList<CandidateProgramResponse> Programs,
    IReadOnlyList<CandidateEducationResponse> Education,
    IReadOnlyList<CandidateExperienceResponse> Experience,
    IReadOnlyList<CandidateSkillResponse> Skills,
    IReadOnlyList<CandidateDocumentResponse> Documents);

/// <summary>
/// Wire representations of the candidate's date-only metadata. An absent date is the
/// empty string on the wire and null in storage; it is never turned into today.
/// </summary>
public static class CandidateDates
{
    public static string ToWire(DateOnly? value) => value?.ToString("yyyy-MM-dd") ?? string.Empty;

    public static bool TryFromWire(string? value, out DateOnly? parsed)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            parsed = null;
            return true;
        }
        if (DateOnly.TryParse(value, System.Globalization.CultureInfo.InvariantCulture, out var date))
        {
            parsed = date;
            return true;
        }
        parsed = null;
        return false;
    }
}

/// <summary>Stable error codes for the candidate slices, with their Spanish messages.</summary>
public static class CandidateErrors
{
    public const string NotFound = "candidate.not_found";
    public const string FirstNameRequired = "candidate.first_name.required";
    public const string LastNameRequired = "candidate.last_name.required";
    public const string StatusInvalid = "candidate.status.invalid";
    public const string DateInvalid = "candidate.date.invalid";
    public const string ConcurrencyConflict = "candidate.concurrency.conflict";
    public const string CatalogValueUnknown = "candidate.catalog_value.unknown";
    public const string RelationDuplicate = "candidate.relation.duplicate";
    public const string LanguageDuplicate = "candidate.language.duplicate";
    public const string ProgramDuplicate = "candidate.program.duplicate";
    public const string SkillDuplicate = "candidate.skill.duplicate";
    public const string RemovedCandidate = "candidate.removed";
    public const string DocumentPrimaryAmbiguous = "candidate.document.primary_ambiguous";
    public const string ConstraintViolation = "candidate.constraint.violation";

    public const string NotFoundMessage = "Candidato no encontrado.";
    public const string FirstNameRequiredMessage = "El nombre es obligatorio.";
    public const string LastNameRequiredMessage = "Los apellidos son obligatorios.";
    public const string StatusInvalidMessage = "El estado del candidato no es válido.";
    public const string DateInvalidMessage = "La fecha no es válida.";
    public const string ConcurrencyConflictMessage =
        "El candidato ha cambiado desde que se cargó. Vuelva a cargarlo e inténtelo de nuevo.";
    public const string CatalogValueUnknownMessage =
        "El valor indicado no existe en el catálogo correspondiente.";
    public const string RelationDuplicateMessage = "El candidato ya tiene este valor registrado.";
    public const string RemovedCandidateMessage =
        "No se puede modificar un candidato desactivado. Reactívelo primero.";
    public const string DocumentPrimaryAmbiguousMessage =
        "Solo puede haber un documento principal por candidato.";
    public const string ConstraintViolationMessage = "La solicitud contiene datos no válidos.";
}

internal static class CandidateGuards
{
    public static void RequireRead(ICurrentActor actor) => Require(actor, Permissions.CandidatesRead);

    public static void RequireCreate(ICurrentActor actor) => Require(actor, Permissions.CandidatesCreate);

    public static void RequireUpdate(ICurrentActor actor) => Require(actor, Permissions.CandidatesUpdate);

    /// <summary>Governs logical removal and restoration alike.</summary>
    public static void RequireDelete(ICurrentActor actor) => Require(actor, Permissions.CandidatesDelete);

    private static void Require(ICurrentActor actor, string permission)
    {
        if (!actor.IsAuthenticated || !actor.HasPermission(permission))
        {
            throw new ForbiddenException();
        }
    }

    public static NotFoundException NotFound() =>
        new(CandidateErrors.NotFound, CandidateErrors.NotFoundMessage);

    public static ConflictException Conflict() =>
        new(CandidateErrors.ConcurrencyConflict, CandidateErrors.ConcurrencyConflictMessage);

    public static RequestValidationException Removed() =>
        new([new ValidationIssue("IsActive", CandidateErrors.RemovedCandidate, CandidateErrors.RemovedCandidateMessage)]);

    public static RequestValidationException UnknownCatalogValue(string property) =>
        new([new ValidationIssue(property, CandidateErrors.CatalogValueUnknown, CandidateErrors.CatalogValueUnknownMessage)]);

    public static RequestValidationException Duplicate(string property, string code, string message) =>
        new([new ValidationIssue(property, code, message)]);

    public static RequestValidationException PrimaryAmbiguous() =>
        new([new ValidationIssue(
            "Documents",
            CandidateErrors.DocumentPrimaryAmbiguous,
            CandidateErrors.DocumentPrimaryAmbiguousMessage)]);

    public static RequestValidationException ConstraintViolation() =>
        new([new ValidationIssue(
            "Request",
            CandidateErrors.ConstraintViolation,
            CandidateErrors.ConstraintViolationMessage)]);

    /// <summary>
    /// Translates a save outcome into the caller-visible refusal. A constraint violation
    /// becomes a generic validation failure: the database's own message would name
    /// columns and constraints, which is internal detail the caller must not receive.
    /// </summary>
    public static Exception ToException(CandidateSaveOutcome outcome) => outcome switch
    {
        CandidateSaveOutcome.ConcurrencyConflict => Conflict(),
        CandidateSaveOutcome.LanguageDuplicate => Duplicate(
            "Language", CandidateErrors.LanguageDuplicate, "El candidato ya tiene este idioma registrado."),
        CandidateSaveOutcome.ProgramDuplicate => Duplicate(
            "Program", CandidateErrors.ProgramDuplicate, "El candidato ya tiene este programa registrado."),
        CandidateSaveOutcome.SkillDuplicate => Duplicate(
            "Skill", CandidateErrors.SkillDuplicate, "El candidato ya tiene esta habilidad registrada."),
        _ => ConstraintViolation(),
    };
}

internal static class CandidateValidators
{
    public static IRuleBuilderOptions<T, string> MustBeAPermittedStatus<T>(this IRuleBuilder<T, string> rule) =>
        rule.Must(CandidateStatuses.IsKnown)
            .WithErrorCode(CandidateErrors.StatusInvalid)
            .WithMessage(CandidateErrors.StatusInvalidMessage);

    public static IRuleBuilderOptions<T, string> MustBeAWireDate<T>(this IRuleBuilder<T, string?> rule) =>
        rule.Must(value => CandidateDates.TryFromWire(value, out _))
            .WithErrorCode(CandidateErrors.DateInvalid)
            .WithMessage(CandidateErrors.DateInvalidMessage)!;
}
