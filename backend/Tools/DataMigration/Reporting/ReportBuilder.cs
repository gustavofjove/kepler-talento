using KeplerTalento.Domain.Operations;
using KeplerTalento.Tools.DataMigration.Export;
using KeplerTalento.Tools.DataMigration.Loading;
using KeplerTalento.Tools.DataMigration.Validation;

namespace KeplerTalento.Tools.DataMigration.Reporting;

/// <summary>
/// Turns a run's outcome into the reconciliation report.
/// </summary>
/// <remarks>
/// Every method here takes a <see cref="RowProblem"/> or a count. None takes a field value,
/// and there is no overload that would accept one — which is what keeps candidate personal
/// data out of the report by construction rather than by care.
/// </remarks>
public static class ReportBuilder
{
    private static readonly IReadOnlyDictionary<string, string> EntityFiles =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [MigrationEntities.Candidate] = ExportContract.Candidates,
            [MigrationEntities.Language] = ExportContract.Languages,
            [MigrationEntities.Program] = ExportContract.Programs,
            [MigrationEntities.Education] = ExportContract.Education,
            [MigrationEntities.Experience] = ExportContract.Experience,
            [MigrationEntities.Skill] = ExportContract.Skills,
            [MigrationEntities.Document] = ExportContract.Documents,
        };

    public static ReconciliationReport Build(
        MigrationRun run,
        ExportSet exportSet,
        LoadResult result,
        DateTimeOffset finishedAtUtc)
    {
        var entities = EntityFiles
            .Select(pair => new ReconciliationReport.EntityTally(
                pair.Key,
                exportSet.RowCount(pair.Value),
                result.Count(pair.Key, RowOutcome.Loaded),
                result.Count(pair.Key, RowOutcome.Rejected),
                result.Count(pair.Key, RowOutcome.Skipped)))
            .OrderBy(tally => tally.Entity, StringComparer.Ordinal)
            .ToList();

        var skippedKeys = result.Problems
            .Where(problem => problem.ReasonCode == ReasonCodes.ApplicationChanged)
            .Select(problem => new ReconciliationReport.SkippedRow(
                problem.Entity, problem.SourceKey, problem.ReasonCode))
            .OrderBy(row => row.SourceKey, StringComparer.Ordinal)
            .ToList();

        var rejected = result.Problems
            .Where(problem => problem.ReasonCode != ReasonCodes.ApplicationChanged)
            .Select(problem => new ReconciliationReport.RejectedRow(
                problem.Entity, problem.SourceKey, problem.Field, problem.ReasonCode))
            .OrderBy(row => row.Entity, StringComparer.Ordinal)
            .ThenBy(row => row.SourceKey, StringComparer.Ordinal)
            .ThenBy(row => row.Field, StringComparer.Ordinal)
            .ToList();

        var documents = BuildDocumentVerifications(exportSet, result);

        return new ReconciliationReport
        {
            RunId = run.Id,
            Verb = run.Verb,
            StartedAtUtc = run.StartedAtUtc,
            FinishedAtUtc = finishedAtUtc,
            Outcome = entities.All(tally => tally.Reconciles)
                ? MigrationOutcomes.Reconciled
                : MigrationOutcomes.NotReconciled,
            PreMigrationBackup = run.BackupLabel,
            Reconciles = entities.All(tally => tally.Reconciles),
            OverwrittenApplicationEdits = result.OverwrittenApplicationEdits,
            Entities = entities,
            Rejected = rejected,
            Skipped = skippedKeys,
            Unresolved = [.. result.UnresolvedValues.Select(value =>
                new ReconciliationReport.UnresolvedEntry(value.Family, value.Value, value.Occurrences))],
            BrokenMappings = [.. result.BrokenMappings.Select(value =>
                new ReconciliationReport.UnresolvedEntry(value.Family, value.Value, value.Occurrences))],
            UnmatchedTargetRecords = [.. result.UnmatchedTargetRecords.Select(record =>
                new ReconciliationReport.UnmatchedEntry(record.Entity, record.SourceKey))],
            Documents = documents,
            Checklist = BuildChecklist(run, result),
        };
    }

    private static List<ReconciliationReport.DocumentVerification> BuildDocumentVerifications(
        ExportSet exportSet,
        LoadResult result)
    {
        var reasons = result.Problems
            .Where(problem => problem.Entity == MigrationEntities.Document)
            .GroupBy(problem => problem.SourceKey, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First().ReasonCode, StringComparer.Ordinal);

        return [.. exportSet[ExportContract.Documents].Rows
            .Select(row => row[ExportContract.SourceKeyColumn].Trim())
            .Select(sourceKey => reasons.TryGetValue(sourceKey, out var reason)
                ? new ReconciliationReport.DocumentVerification(sourceKey, "rejected", reason)
                : new ReconciliationReport.DocumentVerification(sourceKey, "matched", null))
            .OrderBy(entry => entry.SourceKey, StringComparer.Ordinal)];
    }

    private static List<string> BuildChecklist(MigrationRun run, LoadResult result)
    {
        var checklist = new List<string>();
        if (result.UnresolvedValues.Count > 0)
        {
            checklist.Add(
                "Decide what each unresolved value means. Add it to the catalog through the "
                + "administration screens, or map it in mappings.csv, then re-run. Nothing is "
                + "created automatically.");
        }
        if (result.BrokenMappings.Count > 0)
        {
            checklist.Add(
                "Some mappings name a catalog code that does not exist. Correct mappings.csv "
                + "and re-run.");
        }
        if (result.UnmatchedTargetRecords.Count > 0)
        {
            checklist.Add(
                "Some stored records carry a source key this export does not contain. Confirm "
                + "whether they were deleted in Access or merely excluded from the export "
                + "query. Nothing was changed about them.");
        }
        if (result.Problems.Any(problem => problem.ReasonCode == ReasonCodes.ApplicationChanged))
        {
            checklist.Add(
                "Some records were changed in the application after they were loaded and were "
                + "left as they are. Re-run with --overwrite-app-edits only if the Access "
                + "values should win.");
        }
        if (run.Verb == MigrationVerbs.Load)
        {
            checklist.Add(
                $"To undo this run, restore the backup labelled '{run.BackupLabel}' as described "
                + "in docs/BACKUP_RESTORE_ROLLBACK_RUNBOOK.md.");
            checklist.Add(
                "Destroy the export set, including its files directory and any intermediate "
                + "hash file, once this report has been reviewed and filed.");
        }
        return checklist;
    }
}
