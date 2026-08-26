using FluentValidation;
using KeplerTalento.Application.Abstractions.Identity;
using KeplerTalento.Application.Abstractions.Persistence;
using MediatR;

namespace KeplerTalento.Application.Features.Catalogs;

public sealed record ListCatalogFamilyQuery(string Family, bool IncludeInactive)
    : IRequest<IReadOnlyList<CatalogItemResponse>>;

public sealed class ListCatalogFamilyValidator : AbstractValidator<ListCatalogFamilyQuery>
{
    public ListCatalogFamilyValidator()
    {
        RuleFor(query => query.Family).MustBeAKnownFamily();
    }
}

public sealed class ListCatalogFamilyHandler(ICatalogRepository catalogs, ICurrentActor actor)
    : IRequestHandler<ListCatalogFamilyQuery, IReadOnlyList<CatalogItemResponse>>
{
    public async Task<IReadOnlyList<CatalogItemResponse>> Handle(
        ListCatalogFamilyQuery request,
        CancellationToken cancellationToken)
    {
        CatalogGuards.RequireRead(actor);
        var items = await catalogs.ListAsync(request.Family, request.IncludeInactive, cancellationToken);
        return items.Select(CatalogItemResponse.From).ToArray();
    }
}
