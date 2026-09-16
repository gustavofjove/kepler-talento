using KeplerTalento.Application.Abstractions.Documents;
using KeplerTalento.Application.Abstractions.Import;
using KeplerTalento.Application.Features.Candidates;
using KeplerTalento.Application.Features.Import;
using KeplerTalento.Application.Import;
using KeplerTalento.Application.Import.Rows;
using KeplerTalento.Domain.Auditing;
using KeplerTalento.Domain.Candidates;
using KeplerTalento.Domain.Import;
using KeplerTalento.Domain.Operations;
using KeplerTalento.Infrastructure.Documents;
using KeplerTalento.Infrastructure.Operations;
using KeplerTalento.Infrastructure.Persistence;
using KeplerTalento.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace KeplerTalento.Infrastructure.Import;

/// <summary>
/// Writes a claimed batch's rows as candidates, a bounded chunk per transaction, resumably.
/// </summary>
/// <remarks>
/// <para>
/// Design D3. A loadable row's candidate, its relations, its audit events and its <c>loaded</c>
/// outcome always share a transaction with each other: either the candidate and the record of
/// having written it both exist, or neither does. Rows are written <see cref="ChunkSize"/> to a
/// transaction rather than one each, which keeps that property while not paying a round trip per
/// row. A resumed commit reads the outcomes already written and skips those rows, and the unique
/// key on (batch, phase, row) means a row can never be written twice even by two workers racing on
/// an expired lease. That is how "no partial batch" holds without a single transaction over two
/// thousand rows: after any interruption, a resumed run finishes exactly the rows the interrupted
/// one did not.
/// </para>
/// <para>
/// The file is re-read and every row re-judged with the same evaluator the dry run used, because
/// the catalog or the candidate table may have changed since validation. The file itself may not:
/// its digest is checked against the one recorded at upload before any row is written.
/// </para>
/// <para>
/// A candidate is built by <see cref="CandidateFactory"/> and audited with
/// <see cref="CandidateAuditEvents.Created"/>, exactly as a direct create. The actor recorded on the
/// audit event is the batch's uploader, since the worker has no request of its own.
/// </para>
/// </remarks>
public sealed class ImportCommitHandler(
    ApplicationDbContext dbContext,
    IDocumentStorageInventory inventory,
    IDocumentStorage storage,
    ImportRunSupport support,
    ILogger<ImportCommitHandler> logger) : IOperationHandler
{
    /// <summary>
    /// Rows per transaction. Small enough that an interruption loses little work — the unfinished
    /// chunk rolls back whole and is redone on resume — and large enough that a two-thousand-row
    /// file is not two thousand round trips.
    /// </summary>
    internal const int ChunkSize = 100;

    public string Type => ImportOperations.Commit;

    public async Task<ScanOperationOutcome> HandleAsync(Operation operation, CancellationToken cancellationToken)
    {
        if (!ImportOperations.TryParseKey(Type, operation.IdempotencyKey, out var batchId))
        {
            return new(false, "import.operation.invalid");
        }
        var batch = await dbContext.ImportBatches.AsNoTracking().SingleOrDefaultAsync(value => value.Id == batchId, cancellationToken);
        if (batch is null)
        {
            return new(false, "import.batch.missing");
        }
        if (batch.State != ImportBatchStates.Committing)
        {
            return new(true, "import.commit.already_decided");
        }

        if (!await storage.AvailableExistsAsync(batch.StorageKey, cancellationToken))
        {
            return await FailAsync(batchId, ImportReasonCodes.FileMissing, cancellationToken);
        }
        var digest = await inventory.ComputeAvailableSha256Async(batch.StorageKey, cancellationToken);
        if (!string.Equals(digest, batch.Sha256, StringComparison.OrdinalIgnoreCase))
        {
            return await FailAsync(batchId, ImportReasonCodes.FileChanged, cancellationToken);
        }

        var written = await dbContext.ImportRowOutcomes
            .AsNoTracking()
            .Where(outcome => outcome.BatchId == batchId && outcome.Phase == ImportPhases.Commit)
            .ToDictionaryAsync(outcome => outcome.RowNumber, cancellationToken);

        try
        {
            var (_, emails) = await support.PrepareAsync(batch, cancellationToken);
            var resolver = await support.CreateResolverAsync(cancellationToken);
            var createdHere = written.Values
                .Where(outcome => outcome.CandidateId is not null)
                .Select(outcome => outcome.CandidateId!.Value)
                .ToList();
            var existing = await support.ExistingEmailsAsync(emails, createdHere, cancellationToken);
            var evaluator = new CandidateImportRowEvaluator(resolver, existing);

            var pending = new List<RowEvaluation>(ChunkSize);
            dbContext.ChangeTracker.Clear();
            await foreach (var row in support.ReadRowsAsync(batch, cancellationToken))
            {
                if (written.TryGetValue(row.RowNumber, out var earlier))
                {
                    // Replay the claim an earlier row made on its address, so the rows after it are
                    // judged exactly as they were in the uninterrupted run.
                    if (earlier.Outcome is ImportRowOutcomes.Loaded or ImportRowOutcomes.Skipped)
                    {
                        evaluator.RecordEarlierLoad(CandidateImportRowEvaluator.NormalizeEmail(row[CandidateImportContract.Email]));
                    }
                    continue;
                }
                var evaluation = evaluator.Evaluate(row);
                StageRow(batch, operation.CorrelationId, evaluation);
                pending.Add(evaluation);
                if (pending.Count == ChunkSize)
                {
                    await FlushAsync(batch, operation.CorrelationId, pending, cancellationToken);
                }
            }
            await FlushAsync(batch, operation.CorrelationId, pending, cancellationToken);
        }
        catch (ImportStructuralException structural)
        {
            // The digest matched, so this is a contract or limit change between validation and
            // commit. Rows already written stay written and accounted for.
            return await FailAsync(batchId, structural.Code, cancellationToken);
        }

        dbContext.ChangeTracker.Clear();
        var counts = await dbContext.ImportRowOutcomes
            .AsNoTracking()
            .Where(outcome => outcome.BatchId == batchId && outcome.Phase == ImportPhases.Commit)
            .GroupBy(outcome => outcome.Outcome)
            .Select(group => new { Outcome = group.Key, Count = group.Count() })
            .ToDictionaryAsync(entry => entry.Outcome, entry => entry.Count, cancellationToken);
        var committing = await dbContext.ImportBatches.SingleAsync(value => value.Id == batchId, cancellationToken);
        committing.CompleteCommit(
            counts.GetValueOrDefault(ImportRowOutcomes.Loaded),
            counts.GetValueOrDefault(ImportRowOutcomes.Rejected),
            counts.GetValueOrDefault(ImportRowOutcomes.Skipped),
            DateTimeOffset.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation(
            "Import batch {BatchId} committed: {LoadedRows} loaded, {RejectedRows} rejected, {SkippedRows} skipped",
            batchId,
            committing.LoadedRows,
            committing.RejectedRows,
            committing.SkippedRows);
        return new(true, "import.commit.completed");
    }

    /// <summary>
    /// Writes the staged rows as one transaction. If the database refuses any of them, the chunk
    /// is rolled back whole and replayed one row per transaction, so the refused row is rejected
    /// with its reason and every other row still loads.
    /// </summary>
    private async Task FlushAsync(
        ImportBatch batch,
        string correlationId,
        List<RowEvaluation> pending,
        CancellationToken cancellationToken)
    {
        if (pending.Count == 0)
        {
            return;
        }
        try
        {
            // One SaveChanges is one transaction: the candidates, relations, audit events and
            // outcomes of the whole chunk are stored together, or none of them is.
            await dbContext.SaveChangesAsync(cancellationToken);
            dbContext.ChangeTracker.Clear();
        }
        catch (DbUpdateException)
        {
            dbContext.ChangeTracker.Clear();
            foreach (var evaluation in pending)
            {
                StageRow(batch, correlationId, evaluation);
                await SaveRowAsync(batch.Id, evaluation, cancellationToken);
                dbContext.ChangeTracker.Clear();
            }
        }
        logger.LogDebug("Import batch {BatchId} wrote rows up to {RowNumber}", batch.Id, pending[^1].RowNumber);
        pending.Clear();
    }

    private void StageRow(ImportBatch batch, string correlationId, RowEvaluation evaluation)
    {
        var now = DateTimeOffset.UtcNow;
        if (evaluation.Outcome != RowOutcome.Loaded)
        {
            dbContext.ImportRowOutcomes.Add(ImportValidationHandler.ToOutcome(batch.Id, ImportPhases.Commit, evaluation, null, now));
            return;
        }

        var candidate = CandidateFactory.Create(evaluation.Command!, now);
        dbContext.Candidates.Add(candidate);
        var subjectId = candidate.Id.ToString("N");
        // The same event a direct create records, carrying the same actor slot the candidate
        // repository fills.
        dbContext.AuditEvents.Add(new AuditEvent(
            Guid.CreateVersion7(),
            CandidateAuditEvents.Created,
            subjectId,
            correlationId,
            now,
            batch.ActorExternalKey));
        if (evaluation.Languages.Count > 0)
        {
            var languages = evaluation.Languages
                .Select(language => new CandidateLanguage(Guid.CreateVersion7(), candidate.Id, language.LanguageId, language.LevelId))
                .ToList();
            candidate.ReplaceLanguages(languages, now);
            foreach (var language in languages)
            {
                dbContext.Add(language);
            }
            dbContext.AuditEvents.Add(new AuditEvent(
                Guid.CreateVersion7(),
                CandidateAuditEvents.RelationsChanged,
                subjectId,
                correlationId,
                now,
                batch.ActorExternalKey));
        }
        dbContext.ImportRowOutcomes.Add(ImportValidationHandler.ToOutcome(batch.Id, ImportPhases.Commit, evaluation, candidate.Id, now));
    }

    /// <summary>
    /// Saves one row's transaction. Returns false when the row was not loaded as planned; the
    /// database's own message is never logged, because a constraint violation's detail quotes the
    /// offending value.
    /// </summary>
    private async Task<bool> SaveRowAsync(Guid batchId, RowEvaluation evaluation, CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException postgres
            && postgres.SqlState == PostgresErrorCodes.UniqueViolation
            && postgres.ConstraintName == ImportRowOutcomeConfiguration.UniqueRowIndex)
        {
            // Another worker wrote this row first. Its candidate and ours cannot both exist: ours
            // rolled back with this transaction.
            dbContext.ChangeTracker.Clear();
            logger.LogWarning(
                "Import batch {BatchId} row {RowNumber} was already written by a concurrent attempt",
                batchId,
                evaluation.RowNumber);
            return false;
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException postgres
            && postgres.SqlState is PostgresErrorCodes.UniqueViolation or PostgresErrorCodes.CheckViolation or PostgresErrorCodes.ForeignKeyViolation)
        {
            // The database refused the candidate a direct write would also have been refused.
            // Rejected with a reason, never loaded in a weakened form.
            dbContext.ChangeTracker.Clear();
            logger.LogWarning(
                "Import batch {BatchId} row {RowNumber} refused by constraint {ConstraintName} with {ReasonCode}",
                batchId,
                evaluation.RowNumber,
                postgres.ConstraintName,
                ImportReasonCodes.CandidateRefused);
            dbContext.ImportRowOutcomes.Add(new ImportRowOutcome(
                Guid.CreateVersion7(),
                batchId,
                ImportPhases.Commit,
                evaluation.RowNumber,
                ImportRowOutcomes.Rejected,
                "row",
                ImportReasonCodes.CandidateRefused,
                null,
                DateTimeOffset.UtcNow));
            await dbContext.SaveChangesAsync(cancellationToken);
            return false;
        }
    }

    private async Task<ScanOperationOutcome> FailAsync(Guid batchId, string code, CancellationToken cancellationToken)
    {
        dbContext.ChangeTracker.Clear();
        var batch = await dbContext.ImportBatches.SingleAsync(value => value.Id == batchId, cancellationToken);
        batch.Fail(code, detail: null, DateTimeOffset.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogWarning("Import batch {BatchId} commit failed with code {FailureCode}", batchId, code);
        return new(true, code);
    }
}
