using FluentValidation;
using KeplerTalento.Application.Abstractions.Identity;
using KeplerTalento.Application.Abstractions.Persistence;
using MediatR;

namespace KeplerTalento.Application.Features.Import;

public sealed record ListImportBatchesQuery(int Page, int PageSize) : IRequest<ImportBatchPageResponse>;

public sealed class ListImportBatchesValidator : AbstractValidator<ListImportBatchesQuery>
{
    public ListImportBatchesValidator()
    {
        RuleFor(query => query.Page).InclusiveBetween(1, 10_000);
        RuleFor(query => query.PageSize).InclusiveBetween(1, 100);
    }
}

/// <summary>The server-owned batch history, most recent first.</summary>
public sealed class ListImportBatchesHandler(
    IImportBatchRepository batches,
    ICurrentActor actor) : IRequestHandler<ListImportBatchesQuery, ImportBatchPageResponse>
{
    public async Task<ImportBatchPageResponse> Handle(ListImportBatchesQuery request, CancellationToken cancellationToken)
    {
        ImportGuards.RequireImport(actor);
        var page = await batches.ListAsync(request.Page, request.PageSize, cancellationToken);
        var commits = await batches.EarliestCommitByHashAsync(page.Items.Select(batch => batch.Sha256), cancellationToken);
        return new ImportBatchPageResponse(
            [.. page.Items.Select(batch => ImportBatchResponse.From(batch, actor, SameFileCommittedAt(commits, batch)))],
            request.Page,
            request.PageSize,
            page.TotalCount);
    }

    internal static DateTimeOffset? SameFileCommittedAt(
        IReadOnlyDictionary<string, DateTimeOffset> commits,
        Domain.Import.ImportBatch batch) =>
        // Only an *earlier* commit of the same content is worth warning about; the first commit
        // of a file is not a re-import of itself.
        commits.TryGetValue(batch.Sha256, out var firstCommittedAt)
            && (batch.CommittedAtUtc is null || firstCommittedAt < batch.CommittedAtUtc)
                ? firstCommittedAt
                : null;
}
