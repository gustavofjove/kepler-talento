using FluentValidation;
using KeplerTalento.Application.Abstractions.Identity;
using KeplerTalento.Application.Abstractions.Persistence;
using KeplerTalento.Domain.Catalogs;
using MediatR;

namespace KeplerTalento.Application.Features.Catalogs;

public sealed record CreateCatalogItemCommand(string Family, string NameEs, string? Code, string? NameEn)
    : IRequest<CatalogItemResponse>;

public sealed class CreateCatalogItemValidator : AbstractValidator<CreateCatalogItemCommand>
{
    public CreateCatalogItemValidator()
    {
        RuleFor(command => command.Family).MustBeAKnownFamily();
        RuleFor(command => command.NameEs).MustBeAUsableName();
    }
}

public sealed class CreateCatalogItemHandler(ICatalogRepository catalogs, ICurrentActor actor)
    : IRequestHandler<CreateCatalogItemCommand, CatalogItemResponse>
{
    public async Task<CatalogItemResponse> Handle(
        CreateCatalogItemCommand request,
        CancellationToken cancellationToken)
    {
        CatalogGuards.RequireManage(actor);
        var existing = await catalogs.ListAsync(request.Family, includeInactive: true, cancellationToken);
        var nameEs = request.NameEs.Trim();
        var normalized = CatalogName.Normalize(nameEs);
        if (existing.Any(item => item.NameNormalized == normalized))
        {
            throw CatalogGuards.DuplicateName();
        }
        var createdAtUtc = DateTimeOffset.UtcNow;
        var item = new CatalogItem(
            Guid.CreateVersion7(),
            request.Family,
            UniqueCode(existing, nameEs, request.Code),
            nameEs,
            request.NameEn,
            NextSortOrder(existing),
            createdAtUtc);
        catalogs.Add(item);
        var outcome = await catalogs.SaveAsync(
            CatalogAuditEvents.Created,
            item.Id.ToString("N"),
            cancellationToken);
        return outcome switch
        {
            CatalogSaveOutcome.Saved => CatalogItemResponse.From(item),
            // The unique index is the race backstop for the pre-check above.
            _ => throw CatalogGuards.DuplicateName(),
        };
    }

    private static int NextSortOrder(IReadOnlyList<CatalogItem> existing) =>
        existing.Count == 0 ? 1 : existing.Max(item => item.SortOrder) + 1;

    private static string UniqueCode(
        IReadOnlyList<CatalogItem> existing,
        string nameEs,
        string? explicitCode)
    {
        var preferred = (string.IsNullOrWhiteSpace(explicitCode)
            ? CatalogName.DeriveCode(nameEs)
            : explicitCode.Trim().ToUpperInvariant());
        bool Taken(string value) =>
            existing.Any(item => string.Equals(item.Code, value, StringComparison.OrdinalIgnoreCase));
        if (!Taken(preferred))
        {
            return preferred;
        }
        var suffix = 2;
        while (Taken($"{preferred}_{suffix}"))
        {
            suffix++;
        }
        return $"{preferred}_{suffix}";
    }
}
