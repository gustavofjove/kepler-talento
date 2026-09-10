using KeplerTalento.Application.Abstractions.Documents;
using KeplerTalento.Domain.Operations;
using KeplerTalento.Infrastructure.Documents;
using KeplerTalento.Infrastructure.Persistence;
using KeplerTalento.Tools.DataMigration.Export;
using KeplerTalento.Tools.DataMigration.Loading;
using KeplerTalento.Tools.DataMigration.Validation;
using KeplerTalento.Tools.DataMigration.Reporting;
using KeplerTalento.Tools.DataMigration.Resolution;
using KeplerTalento.Tools.DataMigration.Staging;
using Microsoft.EntityFrameworkCore;

namespace KeplerTalento.Tools.DataMigration;

/// <summary>
/// Drives one invocation end to end: read, stage, validate, resolve, load, report.
/// </summary>
public sealed class MigrationRunner(
    MigrationCommandLine commandLine,
    IDocumentStorage storage,
    IDocumentContentInspector inspector,
    IMalwareScanner scanner,
    long maximumDocumentBytes,
    TextWriter output,
    TextWriter error)
{
    private ApplicationDbContext NewContext() =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(commandLine.ConnectionString)
            .Options);

    public async Task<int> RunAsync(MigrationRun run, CancellationToken cancellationToken)
    {
        if (commandLine.Verb == MigrationVerbs.Report)
        {
            return await ReEmitAsync(cancellationToken);
        }

        if (!new ExportReader().TryRead(commandLine.ExportDirectory!, out var exportSet, out var structural))
        {
            error.WriteLine("The export set does not match the documented contract:");
            foreach (var problem in structural)
            {
                error.WriteLine($"  {problem}");
            }
            error.WriteLine("Nothing was written. See docs/ktl-7/access-export-procedure.md.");
            await FinishAsync(run, MigrationOutcomes.Failed, default, null, cancellationToken);
            return ExitCodes.Failed;
        }

        if (!MappingFile.TryRead(commandLine.MappingFile, out var mappings, out var mappingProblems))
        {
            error.WriteLine("The mapping file could not be used:");
            foreach (var problem in mappingProblems)
            {
                error.WriteLine($"  {problem}");
            }
            await FinishAsync(run, MigrationOutcomes.Failed, default, null, cancellationToken);
            return ExitCodes.Failed;
        }

        var staging = new StagingSchema(commandLine.ConnectionString);
        var stagingSurvives = false;
        LoadResult result;
        try
        {
            await staging.CreateAsync(cancellationToken);
            stagingSurvives = true;
            foreach (var file in ExportContract.Files)
            {
                await staging.LoadAsync(file, exportSet[file], cancellationToken);
            }

            var resolver = await BuildResolverAsync(mappings, cancellationToken);
            var loader = new MigrationLoader(NewContext, resolver);
            var isDryRun = commandLine.Verb == MigrationVerbs.Validate;
            result = await loader.LoadAsync(
                exportSet,
                new LoadOptions(commandLine.OverwriteApplicationEdits, DateTimeOffset.UtcNow)
                {
                    DryRun = isDryRun,
                },
                cancellationToken);

            var documents = new DocumentMigrator(
                NewContext, storage, inspector, scanner, maximumDocumentBytes);
            if (isDryRun)
            {
                // Verified, not moved: an operator wants to hear about a bad hash or an
                // infected CV before committing, and none of those checks needs the binary
                // to be in private storage first.
                await documents.VerifyAsync(
                    exportSet,
                    result.SourceKeys(MigrationEntities.Candidate, RowOutcome.Loaded)
                        .ToHashSet(StringComparer.Ordinal),
                    result,
                    cancellationToken);
            }
            else
            {
                var candidateIds = await loader.LoadedCandidateIdsAsync(result, cancellationToken);
                await documents.MigrateAsync(exportSet, candidateIds, result, cancellationToken);
            }

            await staging.DropAsync(cancellationToken);
            stagingSurvives = false;
        }
        finally
        {
            if (stagingSurvives)
            {
                // Retained for diagnosis, never silently: the operator has to know it holds
                // the same personal data the export does.
                error.WriteLine(
                    $"The staging schema \"{StagingSchema.SchemaName}\" was left in place for diagnosis. "
                    + "It holds personal data and must be dropped once you are done with it: "
                    + $"DROP SCHEMA {StagingSchema.SchemaName} CASCADE;");
            }
        }

        var finishedAtUtc = DateTimeOffset.UtcNow;
        var report = ReportBuilder.Build(run, exportSet, result, finishedAtUtc);
        var (jsonPath, markdownPath) = await ReportWriter.WriteAsync(
            report, commandLine.ResolvedOutputDirectory, cancellationToken);

        await FinishAsync(
            run,
            report.Outcome,
            result.ToCounts(exportSet.TotalRows),
            ReportWriter.ToJson(report),
            cancellationToken);

        Summarize(report, jsonPath, markdownPath);
        return report.Reconciles ? ExitCodes.Success : ExitCodes.NotReconciled;
    }

    private void Summarize(ReconciliationReport report, string jsonPath, string markdownPath)
    {
        foreach (var tally in report.Entities)
        {
            output.WriteLine(
                $"  {tally.Entity,-12} source {tally.SourceRows,5}  loaded {tally.Loaded,5}  "
                + $"rejected {tally.Rejected,5}  skipped {tally.Skipped,5}");
        }
        if (report.Unresolved.Count > 0)
        {
            output.WriteLine($"  {report.Unresolved.Count} unresolved reference value(s) need a decision.");
        }
        if (report.UnmatchedTargetRecords.Count > 0)
        {
            output.WriteLine(
                $"  {report.UnmatchedTargetRecords.Count} stored record(s) are absent from this export.");
        }
        output.WriteLine(report.Reconciles
            ? "Every source row is accounted for."
            : "This run did NOT reconcile. Treat the load as incomplete.");
        output.WriteLine($"Report: {jsonPath}");
        output.WriteLine($"        {markdownPath}");
    }

    private async Task<CatalogResolver> BuildResolverAsync(
        IReadOnlyDictionary<(string Family, string Value), string> mappings,
        CancellationToken cancellationToken)
    {
        await using var dbContext = NewContext();
        var entries = await dbContext.CatalogItems
            .AsNoTracking()
            .Select(item => new CatalogResolver.CatalogEntry(item.Id, item.Family, item.Code, item.NameEs))
            .ToListAsync(cancellationToken);
        return new CatalogResolver(entries, mappings);
    }

    private async Task<int> ReEmitAsync(CancellationToken cancellationToken)
    {
        await using var dbContext = NewContext();
        var recorded = await dbContext.MigrationRuns
            .AsNoTracking()
            .SingleOrDefaultAsync(value => value.Id == commandLine.RunId, cancellationToken);
        if (recorded?.ReportJson is null)
        {
            error.WriteLine($"No report is recorded for run {commandLine.RunId}.");
            return ExitCodes.Failed;
        }

        var report = ReportWriter.FromJson(recorded.ReportJson);
        var (jsonPath, markdownPath) = await ReportWriter.WriteAsync(
            report, commandLine.ResolvedOutputDirectory, cancellationToken);
        output.WriteLine($"Report: {jsonPath}");
        output.WriteLine($"        {markdownPath}");
        return ExitCodes.Success;
    }

    private async Task FinishAsync(
        MigrationRun run,
        string outcome,
        MigrationRunCounts counts,
        string? reportJson,
        CancellationToken cancellationToken)
    {
        await using var dbContext = NewContext();
        var tracked = await dbContext.MigrationRuns.SingleAsync(value => value.Id == run.Id, cancellationToken);
        tracked.Finish(outcome, counts, reportJson, DateTimeOffset.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
