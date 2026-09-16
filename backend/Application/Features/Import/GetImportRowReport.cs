using FluentValidation;
using KeplerTalento.Application.Abstractions.Identity;
using KeplerTalento.Application.Abstractions.Persistence;
using KeplerTalento.Domain.Import;
using MediatR;

namespace KeplerTalento.Application.Features.Import;

/// <param name="Phase">
/// <c>validation</c> or <c>commit</c>; when absent, the commit report once a commit has started
/// and the validation report before.
/// </param>
public sealed record GetImportRowReportQuery(Guid BatchId, string? Phase, int Page, int PageSize)
    : IRequest<ImportRowReportResponse>;

public sealed class GetImportRowReportValidator : AbstractValidator<GetImportRowReportQuery>
{
    public GetImportRowReportValidator()
    {
        RuleFor(query => query.Phase)
            .Must(phase => phase is null || ImportPhases.All.Contains(phase, StringComparer.Ordinal))
            .WithMessage("La fase indicada no es válida.");
        RuleFor(query => query.Page).InclusiveBetween(1, 10_000);
        RuleFor(query => query.PageSize).InclusiveBetween(1, 500);
    }
}

/// <summary>
/// A batch's per-row report. Row numbers, column names and reason codes; never a value.
/// </summary>
/// <remarks>
/// It still renders after the file has been purged — the outcomes carry no personal data, and
/// they are what an auditor needs once the file is gone.
/// </remarks>
public sealed class GetImportRowReportHandler(
    IImportBatchRepository batches,
    ICurrentActor actor) : IRequestHandler<GetImportRowReportQuery, ImportRowReportResponse>
{
    public async Task<ImportRowReportResponse> Handle(GetImportRowReportQuery request, CancellationToken cancellationToken)
    {
        ImportGuards.RequireImport(actor);
        var batch = await batches.FindAsync(request.BatchId, cancellationToken) ?? throw ImportErrors.NotFound();
        var phase = request.Phase
            ?? (batch.State is ImportBatchStates.Committing or ImportBatchStates.Committed || batch.CommittedAtUtc is not null
                ? ImportPhases.Commit
                : ImportPhases.Validation);
        var page = await batches.ListOutcomesAsync(batch.Id, phase, request.Page, request.PageSize, cancellationToken);
        return new ImportRowReportResponse(
            batch.Id,
            phase,
            [.. page.Items.Select(outcome => new ImportRowOutcomeResponse(
                outcome.RowNumber,
                outcome.Outcome,
                outcome.Field,
                outcome.ReasonCode))],
            request.Page,
            request.PageSize,
            page.TotalCount);
    }
}
