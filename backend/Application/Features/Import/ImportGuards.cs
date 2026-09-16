using KeplerTalento.Application.Abstractions.Identity;
using KeplerTalento.Application.Common.Errors;

namespace KeplerTalento.Application.Features.Import;

/// <summary>
/// Authorization and refusals for the import slice.
/// </summary>
/// <remarks>
/// Every handler calls <see cref="RequireImport"/> first — before validating, before reading a
/// file, before looking a batch up. That order is what stops a caller without the permission
/// learning whether a batch id exists from the difference between "not found" and "forbidden".
/// <c>candidates.create</c> does not imply import, and import does not imply reading candidates.
/// </remarks>
public static class ImportGuards
{
    public static void RequireImport(ICurrentActor actor)
    {
        if (!actor.IsAuthenticated || !actor.HasPermission(Permissions.CandidatesImport))
        {
            throw new ForbiddenException();
        }
    }
}

/// <summary>Stable error codes for the import slice, with their Spanish messages.</summary>
public static class ImportErrors
{
    public const string BatchNotFound = "import.batch.not_found";
    public const string BatchNotReady = "import.batch.not_ready";
    public const string BatchRefused = "import.batch.refused";
    public const string BatchNotValidated = "import.batch.not_validated";
    public const string BatchHasRejectedRows = "import.batch.has_rejected_rows";
    public const string BatchExpired = "import.batch.expired";
    public const string BatchVersionConflict = "import.batch.version_conflict";
    public const string FileMissing = "import.file.missing";
    public const string FileEmpty = "import.file.empty";
    public const string FileTooLarge = "import.file.too_large";
    public const string FileTypeNotAllowed = "import.file.type_not_allowed";

    public static NotFoundException NotFound() =>
        new(BatchNotFound, "El lote de importación indicado no existe.");

    public static ConflictException NotReady() =>
        new(BatchNotReady, "El archivo todavía se está analizando. Espere a que termine el análisis antes de validarlo.");

    public static ConflictException Refused() =>
        new(BatchRefused, "El archivo de este lote fue rechazado y no se puede validar ni confirmar.");

    public static ConflictException NotValidated() =>
        new(BatchNotValidated, "El lote debe validarse antes de confirmarse.");

    public static ConflictException HasRejectedRows() =>
        new(BatchHasRejectedRows, "El lote tiene filas rechazadas. Corrija el archivo y vuelva a subirlo.");

    public static ConflictException Expired() =>
        new(BatchExpired, "El archivo de este lote ya no se conserva.");

    public static ConflictException VersionConflict() =>
        new(BatchVersionConflict, "El lote ha cambiado desde que lo consultó. Vuelva a cargarlo.");

    public static RequestValidationException Validation(string code, string message) =>
        new([new ValidationIssue("File", code, message)]);
}
