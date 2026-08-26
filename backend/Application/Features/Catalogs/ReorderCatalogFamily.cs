using FluentValidation;
using KeplerTalento.Application.Abstractions.Identity;
using KeplerTalento.Application.Abstractions.Persistence;
using KeplerTalento.Application.Common.Errors;
using KeplerTalento.Domain.Catalogs;
using MediatR;

namespace KeplerTalento.Application.Features.Catalogs;

public sealed record ReorderCatalogFamilyCommand(string Family, IReadOnlyList<Guid> OrderedIds)
    : IRequest<IReadOnlyList<CatalogItemResponse>>;

public sealed class ReorderCatalogFamilyValidator : AbstractValidator<ReorderCatalogFamilyCommand>
{
    public ReorderCatalogFamilyValidator()
    {
        RuleFor(command => command.Family).MustBeAKnownFamily();
        RuleFor(command => command.OrderedIds)
            .Must(ids => ids is { Count: > 0 })
            .WithErrorCode(CatalogErrors.ReorderIncomplete)
            .WithMessage(CatalogErrors.ReorderIncompleteMessage);
    }
}

public sealed class ReorderCatalogFamilyHandler(ICatalogRepository catalogs, ICurrentActor actor)
    : IRequestHandler<ReorderCatalogFamilyCommand, IReadOnlyList<CatalogItemResponse>>
{
    public async Task<IReadOnlyList<CatalogItemResponse>> Handle(
        ReorderCatalogFamilyCommand request,
        CancellationToken cancellationToken)
    {
        CatalogGuards.RequireManage(actor);
        var family = await catalogs.ListAsync(request.Family, includeInactive: true, cancellationToken);
        // The new order must cover the family exactly: no omissions, no duplicates, and no
        // identifiers belonging to another family.
        var submitted = request.OrderedIds;
        if (submitted.Count != family.Count
            || submitted.Distinct().Count() != submitted.Count
            || submitted.Any(id => family.All(item => item.Id != id)))
        {
            throw new RequestValidationException([
                new ValidationIssue(
                    nameof(request.OrderedIds),
                    CatalogErrors.ReorderIncomplete,
                    CatalogErrors.ReorderIncompleteMessage),
            ]);
        }
        var updatedAtUtc = DateTimeOffset.UtcNow;
        var byId = family.ToDictionary(item => item.Id);
        for (var index = 0; index < submitted.Count; index++)
        {
            byId[submitted[index]].MoveTo(index + 1, updatedAtUtc);
        }
        var outcome = await catalogs.SaveAsync(
            CatalogAuditEvents.Reordered,
            request.Family,
            cancellationToken);
        if (outcome == CatalogSaveOutcome.ConcurrencyConflict)
        {
            throw CatalogGuards.Conflict();
        }
        return submitted
            .Select(id => CatalogItemResponse.From(byId[id]))
            .ToArray();
    }
}
