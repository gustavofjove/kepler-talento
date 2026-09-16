using KeplerTalento.Application.Abstractions.Identity;
using KeplerTalento.Application.Abstractions.Persistence;
using MediatR;

namespace KeplerTalento.Application.Features.Import;

public sealed record GetImportBatchQuery(Guid BatchId) : IRequest<ImportBatchResponse>;

/// <summary>
/// One batch's state and counts. The page polls this while a batch is in a transient state.
/// </summary>
public sealed class GetImportBatchHandler(
    IImportBatchRepository batches,
    ICurrentActor actor) : IRequestHandler<GetImportBatchQuery, ImportBatchResponse>
{
    public async Task<ImportBatchResponse> Handle(GetImportBatchQuery request, CancellationToken cancellationToken)
    {
        ImportGuards.RequireImport(actor);
        var batch = await batches.FindAsync(request.BatchId, cancellationToken) ?? throw ImportErrors.NotFound();
        var commits = await batches.EarliestCommitByHashAsync([batch.Sha256], cancellationToken);
        return ImportBatchResponse.From(batch, actor, ListImportBatchesHandler.SameFileCommittedAt(commits, batch));
    }
}
