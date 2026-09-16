using FluentValidation;
using KeplerTalento.Application.Abstractions.Correlation;
using KeplerTalento.Application.Abstractions.Identity;
using KeplerTalento.Application.Abstractions.Operations;
using KeplerTalento.Application.Abstractions.Persistence;
using KeplerTalento.Application.Features.Admin.Users;
using KeplerTalento.Domain.Import;
using MediatR;

namespace KeplerTalento.Application.Features.Import;

public sealed record CommitImportBatchCommand(Guid BatchId, uint Version) : IRequest<ImportBatchResponse>;

public sealed class CommitImportBatchValidator : AbstractValidator<CommitImportBatchCommand>
{
    public CommitImportBatchValidator()
    {
        RuleFor(command => command.Version).MustBeAnIssuedVersion();
    }
}

/// <summary>
/// Claims a validated batch for commit and queues the durable operation that writes it.
/// </summary>
/// <remarks>
/// <para>
/// The claim is the <c>validated → committing</c> transition under optimistic concurrency, and
/// the batch id is the idempotency key (design D3): of two concurrent commits exactly one
/// matches the version, and the other is refused as a conflict. A batch already committing or
/// committed answers with its current state and nothing new starts, so committing twice never
/// creates a candidate twice.
/// </para>
/// <para>
/// A batch with rejected rows is refused. The page's two-step shape exists to put a human between
/// "this is what would happen" and "do it", and "fix the file and upload it again" is the
/// documented path — a partial commit would leave the rejected people to be re-imported into a
/// file whose other rows are now duplicates.
/// </para>
/// </remarks>
public sealed class CommitImportBatchHandler(
    IImportBatchRepository batches,
    IOperationRepository operations,
    ICorrelationContext correlation,
    ICurrentActor actor) : IRequestHandler<CommitImportBatchCommand, ImportBatchResponse>
{
    public async Task<ImportBatchResponse> Handle(CommitImportBatchCommand request, CancellationToken cancellationToken)
    {
        ImportGuards.RequireImport(actor);
        var batch = await batches.FindAsync(request.BatchId, cancellationToken) ?? throw ImportErrors.NotFound();
        switch (batch.State)
        {
            case ImportBatchStates.Committing or ImportBatchStates.Committed:
                return ImportBatchResponse.From(batch, actor, sameFileCommittedAt: null);
            case ImportBatchStates.Infected or ImportBatchStates.Unscannable:
                throw ImportErrors.Refused();
            case ImportBatchStates.Expired:
                throw ImportErrors.Expired();
            case ImportBatchStates.Validated:
                break;
            default:
                throw ImportErrors.NotValidated();
        }
        if (batch.RejectedRows > 0)
        {
            throw ImportErrors.HasRejectedRows();
        }

        batches.ExpectVersion(batch, request.Version);
        batch.StartCommit(DateTimeOffset.UtcNow);
        if (await batches.SaveAsync(cancellationToken) != ImportBatchSaveOutcome.Saved)
        {
            throw ImportErrors.VersionConflict();
        }
        await operations.EnqueueAsync(
            ImportOperations.Commit,
            correlation.CorrelationId,
            ImportOperations.Key(ImportOperations.Commit, batch.Id, batch.OperationAttempt),
            cancellationToken);
        return ImportBatchResponse.From(batch, actor, sameFileCommittedAt: null);
    }
}
