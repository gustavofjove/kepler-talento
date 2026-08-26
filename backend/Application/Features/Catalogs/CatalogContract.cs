using FluentValidation;
using KeplerTalento.Application.Abstractions.Identity;
using KeplerTalento.Application.Common.Errors;
using KeplerTalento.Domain.Catalogs;

namespace KeplerTalento.Application.Features.Catalogs;

public sealed record CatalogItemResponse(
    Guid Id,
    string Code,
    string NameEs,
    string? NameEn,
    int SortOrder,
    bool IsActive,
    uint Version)
{
    public static CatalogItemResponse From(CatalogItem item) => new(
        item.Id,
        item.Code,
        item.NameEs,
        item.NameEn,
        item.SortOrder,
        item.IsActive,
        item.Version);
}

/// <summary>Stable error codes for the catalog slices.</summary>
public static class CatalogErrors
{
    public const string FamilyInvalid = "catalog.family.invalid";
    public const string NameRequired = "catalog.name.required";
    public const string NameDuplicate = "catalog.name.duplicate";
    public const string CodeRequired = "catalog.code.required";
    public const string NotFound = "catalog.not_found";
    public const string ReorderIncomplete = "catalog.reorder.incomplete";
    public const string ConcurrencyConflict = "catalog.concurrency.conflict";

    public const string NameRequiredMessage = "El nombre es obligatorio.";
    public const string NameDuplicateMessage = "Ya existe un valor con ese nombre.";
    public const string CodeRequiredMessage = "El código es obligatorio.";
    public const string NotFoundMessage = "No se encontró el elemento del catálogo.";
    public const string FamilyInvalidMessage = "La familia de catálogo no es válida.";
    public const string ReorderIncompleteMessage = "El nuevo orden debe incluir todos los valores de la familia.";
    public const string ConcurrencyConflictMessage =
        "El valor ha cambiado desde que se cargó. Vuelva a cargarlo e inténtelo de nuevo.";
}

internal static class CatalogGuards
{
    public static void RequireRead(ICurrentActor actor)
    {
        if (!actor.IsAuthenticated || !actor.HasPermission(Permissions.CatalogsRead))
        {
            throw new ForbiddenException();
        }
    }

    public static void RequireManage(ICurrentActor actor)
    {
        if (!actor.IsAuthenticated || !actor.HasPermission(Permissions.CatalogsManage))
        {
            throw new ForbiddenException();
        }
    }

    public static NotFoundException NotFound() =>
        new(CatalogErrors.NotFound, CatalogErrors.NotFoundMessage);

    public static ConflictException Conflict() =>
        new(CatalogErrors.ConcurrencyConflict, CatalogErrors.ConcurrencyConflictMessage);

    public static RequestValidationException DuplicateName() =>
        new([new ValidationIssue("NameEs", CatalogErrors.NameDuplicate, CatalogErrors.NameDuplicateMessage)]);
}

internal static class CatalogValidators
{
    public static IRuleBuilderOptions<T, string> MustBeAKnownFamily<T>(this IRuleBuilder<T, string> rule) =>
        rule.Must(CatalogFamilies.IsKnown)
            .WithErrorCode(CatalogErrors.FamilyInvalid)
            .WithMessage(CatalogErrors.FamilyInvalidMessage);

    public static IRuleBuilderOptions<T, string> MustBeAUsableName<T>(this IRuleBuilder<T, string> rule) =>
        rule.Must(value => !string.IsNullOrWhiteSpace(value))
            .WithErrorCode(CatalogErrors.NameRequired)
            .WithMessage(CatalogErrors.NameRequiredMessage);
}
