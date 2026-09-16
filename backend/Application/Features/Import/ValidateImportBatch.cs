using FluentValidation;
using KeplerTalento.Application.Abstractions.Correlation;
using KeplerTalento.Application.Abstractions.Identity;
using KeplerTalento.Application.Abstractions.Operations;
using KeplerTalento.Application.Abstractions.Persistence;
using KeplerTalento.Application.Features.Admin.Users;
using KeplerTalento.Domain.Import;
using MediatR;

namespace KeplerTalento.Application.Features.Import;

public sealed record ValidateImportBatchCommand(Guid BatchId, uint Version) : IRequest<ImportBatchResponse>;

public sealed class ValidateImportBatchValidator : AbstractValidator<ValidateImportBatchCommand>
{
    public ValidateImportBatchValidator()
    {
        RuleFor(command => command.Version).MustBeAnIssuedVersion();
    }
}

/// <summary>
/// Starts the dry run. Validation changes no candidate: it reads the file, applies the row rules
/// and writes one outcome per data row.
/// </summary>
/// <remarks>
/// The batch moves <c>scanned → validating</c> under the version the caller read, and the durable
/// operation is recorded before the request reports acceptance. A batch whose file is still in
/// quarantine is refused as not ready, and its content is not touched.
/// </remarks>
public sealed class ValidateImportBatchHandler(
    IImportBatchRepository batches,
    IOperationRepository operations,
    ICorrelationContext correlation,
    ICurrentActor actor) : IRequestHandler<ValidateImportBatchCommand, ImportBatchResponse>
{
    public async Task<ImportBatchResponse> Handle(ValidateImportBatchCommand request, CancellationToken cancellationToken)
    {
        ImportGuards.RequireImport(actor);
        var batch = await batches.FindAsync(request.BatchId, cancellationToken) ?? throw ImportErrors.NotFound();
        switch (batch.State)
        {
            case ImportBatchStates.Uploaded or ImportBatchStates.Scanning:
                throw ImportErrors.NotReady();
            case ImportBatchStates.Infected or ImportBatchStates.Unscannable:
                throw ImportErrors.Refused();
            case ImportBatchStates.Expired:
                throw ImportErrors.Expired();
            case ImportBatchStates.Scanned:
                break;
            default:
                // Already validating or past it: answer with where the batch is, and start nothing.
                return ImportBatchResponse.From(batch, actor, sameFileCommittedAt: null);
        }

        batches.ExpectVersion(batch, request.Version);
        batch.StartValidation(DateTimeOffset.UtcNow);
        if (await batches.SaveAsync(cancellationToken) != ImportBatchSaveOutcome.Saved)
        {
            throw ImportErrors.VersionConflict();
        }
        await operations.EnqueueAsync(
            ImportOperations.Validate,
            correlation.CorrelationId,
            ImportOperations.Key(ImportOperations.Validate, batch.Id, batch.OperationAttempt),
            cancellationToken);
        return ImportBatchResponse.From(batch, actor, sameFileCommittedAt: null);
    }
}
