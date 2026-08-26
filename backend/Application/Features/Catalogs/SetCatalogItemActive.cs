using FluentValidation;
using KeplerTalento.Application.Abstractions.Identity;
using KeplerTalento.Application.Abstractions.Persistence;
using KeplerTalento.Domain.Catalogs;
using MediatR;

namespace KeplerTalento.Application.Features.Catalogs;

public sealed record SetCatalogItemActiveCommand(string Family, Guid Id, bool IsActive, uint Version)
    : IRequest<CatalogItemResponse>;

public sealed class SetCatalogItemActiveValidator : AbstractValidator<SetCatalogItemActiveCommand>
{
    public SetCatalogItemActiveValidator()
    {
        RuleFor(command => command.Family).MustBeAKnownFamily();
    }
}

/// <summary>
/// Activation and deactivation are the only removal path for a catalog value: there is no
/// physical delete anywhere in this slice.
/// </summary>
public sealed class SetCatalogItemActiveHandler(ICatalogRepository catalogs, ICurrentActor actor)
    : IRequestHandler<SetCatalogItemActiveCommand, CatalogItemResponse>
{
    public async Task<CatalogItemResponse> Handle(
        SetCatalogItemActiveCommand request,
        CancellationToken cancellationToken)
    {
        CatalogGuards.RequireManage(actor);
        var item = await catalogs.FindAsync(request.Family, request.Id, cancellationToken)
            ?? throw CatalogGuards.NotFound();
        // A value referenced by a candidate relation may still be deactivated, so that it
        // stops being offered while existing candidate records keep their meaning. The lookup
        // is consulted here for the record; it becomes load-bearing when candidate relations
        // move server-side in KTL-8.
        _ = await catalogs.IsValueInUseAsync(request.Family, item.NameNormalized, cancellationToken);
        catalogs.ExpectVersion(item, request.Version);
        item.SetActive(request.IsActive, DateTimeOffset.UtcNow);
        var outcome = await catalogs.SaveAsync(
            CatalogAuditEvents.ActivationChanged,
            item.Id.ToString("N"),
            cancellationToken);
        if (outcome == CatalogSaveOutcome.ConcurrencyConflict)
        {
            throw CatalogGuards.Conflict();
        }
        return CatalogItemResponse.From(item);
    }
}
