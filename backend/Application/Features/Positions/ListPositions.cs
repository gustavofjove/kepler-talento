using KeplerTalento.Application.Abstractions.Identity;
using KeplerTalento.Application.Abstractions.Persistence;
using KeplerTalento.Application.Common.Errors;
using KeplerTalento.Domain.Positions;
using MediatR;

namespace KeplerTalento.Application.Features.Positions;

public sealed record ListPositionsQuery(
    string? Status, string? Text, int? Page, int? PageSize, string? SortField, string? SortDirection)
    : IRequest<PositionPageResponse>;

public sealed class ListPositionsHandler(IPositionRepository positions, ICurrentActor actor)
    : IRequestHandler<ListPositionsQuery, PositionPageResponse>
{
    private static readonly string[] SortFields = ["title", "location", "status", "updatedAt"];
    public async Task<PositionPageResponse> Handle(ListPositionsQuery request, CancellationToken cancellationToken)
    {
        PositionGuards.RequireRead(actor);
        var status = string.IsNullOrWhiteSpace(request.Status) ? PositionStatuses.Open : request.Status.Trim();
        var page = request.Page ?? 1;
        var pageSize = request.PageSize ?? 25;
        var sortField = string.IsNullOrWhiteSpace(request.SortField) ? "updatedAt" : request.SortField.Trim();
        var direction = string.IsNullOrWhiteSpace(request.SortDirection) ? "desc" : request.SortDirection.Trim().ToLowerInvariant();
        var issues = new List<ValidationIssue>();
        if (status is not (PositionStatuses.Open or PositionStatuses.Closed or "all")) issues.Add(new("Status", PositionErrors.StatusInvalid, "El estado de la posición no es válido."));
        if (page < 1) issues.Add(new("Page", PositionErrors.PageInvalid, "La página debe ser 1 o superior."));
        if (pageSize is < 1 or > 100) issues.Add(new("PageSize", PositionErrors.PageSizeInvalid, "El tamaño de página debe estar entre 1 y 100."));
        if (!SortFields.Contains(sortField, StringComparer.Ordinal)) issues.Add(new("SortField", PositionErrors.SortFieldInvalid, "El campo de ordenación no es válido."));
        if (direction is not ("asc" or "desc")) issues.Add(new("SortDirection", PositionErrors.SortDirectionInvalid, "La dirección debe ser asc o desc."));
        if ((request.Text ?? string.Empty).Trim().Length > 200) issues.Add(new("Text", PositionErrors.TextTooLong, "El texto de búsqueda es demasiado largo."));
        if (issues.Count > 0) throw new RequestValidationException(issues);
        var result = await positions.ListAsync(new(status, PositionText.Normalize(request.Text), page, pageSize, sortField, direction), cancellationToken);
        return new PositionPageResponse([.. result.Items.Select(item => new PositionListItemResponse(item.Id, item.Title, item.Location, item.Status, item.UpdatedAtUtc, item.Version, item.CandidateCount))], result.Page, result.PageSize, result.TotalCount);
    }
}
