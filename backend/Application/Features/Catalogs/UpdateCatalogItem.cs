using FluentValidation;
using KeplerTalento.Application.Abstractions.Identity;
using KeplerTalento.Application.Abstractions.Persistence;
using KeplerTalento.Domain.Catalogs;
using MediatR;

namespace KeplerTalento.Application.Features.Catalogs;

public sealed record UpdateCatalogItemCommand(
    string Family,
    Guid Id,
    string NameEs,
    string? Code,
    string? NameEn,
    uint Version) : IRequest<CatalogItemResponse>;

public sealed class UpdateCatalogItemValidator : AbstractValidator<UpdateCatalogItemCommand>
{
    public UpdateCatalogItemValidator()
    {
        RuleFor(command => command.Family).MustBeAKnownFamily();
        RuleFor(command => command.NameEs).MustBeAUsableName();
    }
}

public sealed class UpdateCatalogItemHandler(ICatalogRepository catalogs, ICurrentActor actor)
    : IRequestHandler<UpdateCatalogItemCommand, CatalogItemResponse>
{
    public async Task<CatalogItemResponse> Handle(
        UpdateCatalogItemCommand request,
        CancellationToken cancellationToken)
    {
        CatalogGuards.RequireManage(actor);
        var item = await catalogs.FindAsync(request.Family, request.Id, cancellationToken)
            ?? throw CatalogGuards.NotFound();
        var nameEs = request.NameEs.Trim();
        var normalized = CatalogName.Normalize(nameEs);
        var family = await catalogs.ListAsync(request.Family, includeInactive: true, cancellationToken);
        if (family.Any(other => other.Id != item.Id && other.NameNormalized == normalized))
        {
            throw CatalogGuards.DuplicateName();
        }
        catalogs.ExpectVersion(item, request.Version);
        item.Rename(nameEs, request.NameEn, DateTimeOffset.UtcNow);
        if (!string.IsNullOrWhiteSpace(request.Code))
        {
            item.ChangeCode(request.Code, DateTimeOffset.UtcNow);
        }
        var outcome = await catalogs.SaveAsync(
            CatalogAuditEvents.Updated,
            item.Id.ToString("N"),
            cancellationToken);
        return outcome switch
        {
            CatalogSaveOutcome.Saved => CatalogItemResponse.From(item),
            CatalogSaveOutcome.ConcurrencyConflict => throw CatalogGuards.Conflict(),
            _ => throw CatalogGuards.DuplicateName(),
        };
    }
}
