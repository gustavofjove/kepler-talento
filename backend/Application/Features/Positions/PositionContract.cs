using KeplerTalento.Application.Abstractions.Identity;
using KeplerTalento.Application.Abstractions.Positions;
using KeplerTalento.Application.Common.Errors;
using KeplerTalento.Application.Features.Search;
using KeplerTalento.Domain.Positions;

namespace KeplerTalento.Application.Features.Positions;

public sealed record PositionResponse(
    Guid Id,
    string Title,
    string Description,
    string Location,
    string Status,
    SearchFiltersInput Requirements,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    uint Version);

public sealed record PositionListItemResponse(
    Guid Id, string Title, string Location, string Status, DateTimeOffset UpdatedAtUtc, uint Version);

public sealed record PositionPageResponse(
    IReadOnlyList<PositionListItemResponse> Items, int Page, int PageSize, int TotalCount);

public static class PositionErrors
{
    public const string NotFound = "position.not_found";
    public const string TitleRequired = "position.title.required";
    public const string TitleTooLong = "position.title.too_long";
    public const string TitleConflict = "position.title.conflict";
    public const string LocationTooLong = "position.location.too_long";
    public const string DescriptionTooLong = "position.description.too_long";
    public const string StatusInvalid = "position.status.invalid";
    public const string RequirementsInvalid = "position.requirements.invalid";
    public const string VersionInvalid = "position.version.invalid";
    public const string ConcurrencyConflict = "position.concurrency.conflict";
    public const string PageInvalid = "position.page.invalid";
    public const string PageSizeInvalid = "position.page_size.invalid";
    public const string SortFieldInvalid = "position.sort_field.invalid";
    public const string SortDirectionInvalid = "position.sort_direction.invalid";
    public const string TextTooLong = "position.text.too_long";
}

internal static class PositionDescription
{
    public static string Sanitize(IPositionDescriptionSanitizer sanitizer, string? input)
    {
        var sanitized = sanitizer.Sanitize(input ?? string.Empty);
        if (sanitized.Length > PositionText.MaximumDescriptionLength)
        {
            throw new RequestValidationException([new ValidationIssue(
                "Description",
                PositionErrors.DescriptionTooLong,
                "La descripción de la posición es demasiado larga.")]);
        }
        return sanitized;
    }
}

internal static class PositionGuards
{
    public static void RequireRead(ICurrentActor actor) => Require(actor, Permissions.PositionsRead);
    public static void RequireManage(ICurrentActor actor) => Require(actor, Permissions.PositionsManage);
    private static void Require(ICurrentActor actor, string permission)
    {
        if (!actor.IsAuthenticated || !actor.HasPermission(permission)) throw new ForbiddenException();
    }
}

internal static class PositionMapping
{
    public static PositionResponse ToResponse(this Position position)
    {
        var filters = SearchFilterDocument.Parse(
            position.Requirements,
            PositionErrors.RequirementsInvalid,
            "Los requisitos de la posición almacenados no son válidos.");
        return new PositionResponse(
            position.Id, position.Title, position.Description, position.Location, position.Status,
            filters.ToInput(), position.CreatedAtUtc, position.UpdatedAtUtc, position.Version);
    }
}
