using KeplerTalento.Application.Abstractions.Import;
using KeplerTalento.Application.Features.Import;
using KeplerTalento.Application.Import;
using KeplerTalento.Application.Import.Rows;
using KeplerTalento.Domain.Import;
using KeplerTalento.Domain.Operations;
using KeplerTalento.Infrastructure.Documents;
using KeplerTalento.Infrastructure.Operations;
using KeplerTalento.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace KeplerTalento.Infrastructure.Import;

/// <summary>
/// The dry run: reads the admitted file, judges every row, and writes one outcome per data row.
/// It creates, changes and deactivates no candidate.
/// </summary>
/// <remarks>
/// All outcomes and the batch's move to <c>validated</c> are written in one transaction, so a
/// validation interrupted partway leaves nothing behind and its retry starts clean. A structural
/// problem fails the batch with a code and at most a column name; no row is evaluated.
/// </remarks>
public sealed class ImportValidationHandler(
    ApplicationDbContext dbContext,
    ImportRunSupport support,
    ILogger<ImportValidationHandler> logger) : IOperationHandler
{
    public string Type => ImportOperations.Validate;

    public async Task<ScanOperationOutcome> HandleAsync(Operation operation, CancellationToken cancellationToken)
    {
        if (!ImportOperations.TryParseKey(Type, operation.IdempotencyKey, out var batchId))
        {
            return new(false, "import.operation.invalid");
        }
        var batch = await dbContext.ImportBatches.SingleOrDefaultAsync(value => value.Id == batchId, cancellationToken);
        if (batch is null)
        {
            return new(false, "import.batch.missing");
        }
        if (batch.State != ImportBatchStates.Validating)
        {
            return new(true, "import.validation.already_decided");
        }

        var now = DateTimeOffset.UtcNow;
        try
        {
            var (_, emails) = await support.PrepareAsync(batch, cancellationToken);
            var resolver = await support.CreateResolverAsync(cancellationToken);
            var existing = await support.ExistingEmailsAsync(emails, [], cancellationToken);
            var evaluator = new CandidateImportRowEvaluator(resolver, existing);

            var rowCount = 0;
            int loadable = 0, rejected = 0, skipped = 0;
            await foreach (var row in support.ReadRowsAsync(batch, cancellationToken))
            {
                rowCount++;
                var evaluation = evaluator.Evaluate(row);
                switch (evaluation.Outcome)
                {
                    case RowOutcome.Loaded:
                        loadable++;
                        break;
                    case RowOutcome.Rejected:
                        rejected++;
                        break;
                    default:
                        skipped++;
                        break;
                }
                dbContext.ImportRowOutcomes.Add(ToOutcome(batch.Id, ImportPhases.Validation, evaluation, candidateId: null, now));
                if (evaluation.Outcome != RowOutcome.Loaded)
                {
                    logger.LogInformation(
                        "Import batch {BatchId} row {RowNumber} {RowOutcome} on {Field} with {ReasonCode}",
                        batch.Id,
                        evaluation.RowNumber,
                        evaluation.Outcome,
                        evaluation.Field,
                        evaluation.ReasonCode);
                }
            }

            batch.CompleteValidation(
                rowCount,
                loadable,
                rejected,
                skipped,
                ImportBatchResponse.SerializeUnresolved(resolver.UnresolvedValues),
                now);
            await dbContext.SaveChangesAsync(cancellationToken);
            logger.LogInformation(
                "Import batch {BatchId} validated: {RowCount} rows, {LoadableRows} loadable, {RejectedRows} rejected, {SkippedRows} skipped",
                batch.Id,
                rowCount,
                loadable,
                rejected,
                skipped);
            return new(true, "import.validation.completed");
        }
        catch (ImportStructuralException structural)
        {
            dbContext.ChangeTracker.Clear();
            var failed = await dbContext.ImportBatches.SingleAsync(value => value.Id == batchId, cancellationToken);
            failed.Fail(structural.Code, structural.Detail, now);
            await dbContext.SaveChangesAsync(cancellationToken);
            logger.LogInformation(
                "Import batch {BatchId} validation refused the file with code {FailureCode}",
                batch.Id,
                structural.Code);
            return new(true, structural.Code);
        }
    }

    internal static ImportRowOutcome ToOutcome(
        Guid batchId,
        string phase,
        RowEvaluation evaluation,
        Guid? candidateId,
        DateTimeOffset now) => new(
        Guid.CreateVersion7(),
        batchId,
        phase,
        evaluation.RowNumber,
        evaluation.Outcome switch
        {
            RowOutcome.Loaded => ImportRowOutcomes.Loaded,
            RowOutcome.Rejected => ImportRowOutcomes.Rejected,
            _ => ImportRowOutcomes.Skipped,
        },
        evaluation.Field,
        evaluation.ReasonCode,
        candidateId,
        now);
}
