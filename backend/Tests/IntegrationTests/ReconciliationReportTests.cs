using KeplerTalento.Domain.Operations;
using KeplerTalento.Infrastructure.Documents;
using KeplerTalento.Infrastructure.Persistence;
using KeplerTalento.Tools.DataMigration;
using KeplerTalento.Tools.DataMigration.Reporting;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace KeplerTalento.Tests.IntegrationTests;

/// <summary>
/// The whole tool, end to end, and the report it produces.
/// </summary>
public sealed class ReconciliationReportTests(PostgreSqlFixture database)
    : IClassFixture<PostgreSqlFixture>, IDisposable
{
    private readonly string _workspace =
        Path.Combine(Path.GetTempPath(), $"ktl-report-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_workspace))
        {
            Directory.Delete(_workspace, recursive: true);
        }
    }

    private string OutputDirectory => Path.Combine(_workspace, "reports");
    private string StorageRoot => Path.Combine(_workspace, "storage");

    private DbContextOptions<ApplicationDbContext> Options =>
        new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(database.ConnectionString).Options;

    private ApplicationDbContext NewContext() => new(Options);

    private async Task PrepareAsync()
    {
        await using var dbContext = NewContext();
        await DatabaseInitializer.MigrateAsync(dbContext, CancellationToken.None);
        await DatabaseInitializer.SeedCatalogsAsync(dbContext, CancellationToken.None);
        await dbContext.CandidateLanguages.ExecuteDeleteAsync();
        await dbContext.CandidatePrograms.ExecuteDeleteAsync();
        await dbContext.CandidateEducation.ExecuteDeleteAsync();
        await dbContext.CandidateExperience.ExecuteDeleteAsync();
        await dbContext.CandidateSkills.ExecuteDeleteAsync();
        await dbContext.Documents.ExecuteDeleteAsync();
        await dbContext.Candidates.ExecuteDeleteAsync();
        await dbContext.MigrationRuns.ExecuteDeleteAsync();
    }

    private async Task<(int ExitCode, MigrationRun Run, string Output, string Error)> RunAsync(
        string verb = MigrationVerbs.Load,
        string? mappingFile = null)
    {
        var commandLine = new MigrationCommandLine
        {
            Verb = verb,
            ConnectionString = database.ConnectionString,
            ExportDirectory = MigrationFixtures.ExportDirectory,
            MappingFile = mappingFile ?? MigrationFixtures.MappingFile,
            OutputDirectory = OutputDirectory,
            PreMigrationBackup = verb == MigrationVerbs.Load ? "pre-ktl7-2026-09-10" : null,
        };

        var run = new MigrationRun(
            Guid.CreateVersion7(), verb, commandLine.PreMigrationBackup, DateTimeOffset.UtcNow);
        await using (var dbContext = NewContext())
        {
            dbContext.MigrationRuns.Add(run);
            await dbContext.SaveChangesAsync();
        }

        var storageOptions = new DocumentStorageOptions { Root = StorageRoot };
        FileSystemDocumentStorage.ValidateAndPrepare(storageOptions);
        var output = new StringWriter();
        var error = new StringWriter();
        var exitCode = await new MigrationRunner(
            commandLine,
            new FileSystemDocumentStorage(storageOptions),
            new DocumentContentInspector(),
            new MarkerMalwareScanner(),
            storageOptions.MaximumBytes,
            output,
            error).RunAsync(run, CancellationToken.None);

        return (exitCode, run, output.ToString(), error.ToString());
    }

    private async Task<ReconciliationReport> StoredReportAsync(Guid runId)
    {
        await using var dbContext = NewContext();
        var run = await dbContext.MigrationRuns.AsNoTracking().SingleAsync(value => value.Id == runId);
        Assert.NotNull(run.ReportJson);
        return ReportWriter.FromJson(run.ReportJson!);
    }

    [Fact]
    public async Task A_full_run_reconciles_and_writes_both_renderings()
    {
        await PrepareAsync();

        var (exitCode, run, output, _) = await RunAsync();

        Assert.Equal(0, exitCode);
        Assert.Contains("Every source row is accounted for.", output, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(OutputDirectory, $"reconciliation-{run.Id}.json")));
        Assert.True(File.Exists(Path.Combine(OutputDirectory, $"reconciliation-{run.Id}.md")));
    }

    [Fact]
    public async Task Every_source_row_is_accounted_for_in_every_entity()
    {
        await PrepareAsync();
        var (_, run, _, _) = await RunAsync();

        var report = await StoredReportAsync(run.Id);

        Assert.True(report.Reconciles);
        Assert.All(report.Entities, tally => Assert.True(
            tally.Reconciles,
            $"{tally.Entity}: {tally.SourceRows} source rows, "
                + $"{tally.Loaded + tally.Rejected + tally.Skipped} accounted for"));
        Assert.Equal(MigrationOutcomes.Reconciled, report.Outcome);
    }

    [Fact]
    public async Task The_report_names_the_backup_that_undoes_the_run()
    {
        await PrepareAsync();
        var (_, run, _, _) = await RunAsync();

        var report = await StoredReportAsync(run.Id);
        Assert.Equal("pre-ktl7-2026-09-10", report.PreMigrationBackup);

        var markdown = await File.ReadAllTextAsync(Path.Combine(OutputDirectory, $"reconciliation-{run.Id}.md"));
        Assert.Contains("pre-ktl7-2026-09-10", markdown, StringComparison.Ordinal);
        Assert.Contains("BACKUP_RESTORE_ROLLBACK_RUNBOOK.md", markdown, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_report_lists_unresolved_values_so_a_decision_can_be_made()
    {
        await PrepareAsync();
        var (_, run, _, _) = await RunAsync();

        var report = await StoredReportAsync(run.Id);

        var unresolved = Assert.Single(report.Unresolved);
        Assert.Equal("Klingon", unresolved.Value);
        Assert.Equal("language", unresolved.Family);
        Assert.Contains(
            report.Checklist,
            item => item.Contains("mappings.csv", StringComparison.Ordinal));
    }

    [Fact]
    public async Task The_report_records_rejections_by_source_key_and_field_only()
    {
        await PrepareAsync();
        var (_, run, _, _) = await RunAsync();

        var report = await StoredReportAsync(run.Id);

        Assert.Contains(
            report.Rejected,
            row => row.SourceKey == "C-003" && row.Field == "ConsentAt" && row.ReasonCode == "consent.missing");
        Assert.Contains(
            report.Rejected,
            row => row.SourceKey == "C-004" && row.Field == "Email" && row.ReasonCode == "email.invalid");
    }

    [Fact]
    public async Task The_report_verifies_every_document_hash()
    {
        await PrepareAsync();
        var (_, run, _, _) = await RunAsync();

        var report = await StoredReportAsync(run.Id);

        Assert.Equal(3, report.Documents.Count);
        Assert.Contains(report.Documents, entry => entry.SourceKey == "D-001" && entry.Result == "matched");
        Assert.Contains(
            report.Documents,
            entry => entry.SourceKey == "D-006" && entry.ReasonCode == "document.hash.mismatch");
        Assert.Contains(
            report.Documents,
            entry => entry.SourceKey == "D-007" && entry.ReasonCode == "document.scan.rejected");
    }

    [Fact]
    public async Task Neither_rendering_of_the_report_contains_candidate_personal_data()
    {
        await PrepareAsync();
        var (_, run, output, error) = await RunAsync();

        var json = await File.ReadAllTextAsync(Path.Combine(OutputDirectory, $"reconciliation-{run.Id}.json"));
        var markdown = await File.ReadAllTextAsync(Path.Combine(OutputDirectory, $"reconciliation-{run.Id}.md"));

        foreach (var text in new[] { json, markdown, output, error })
        {
            Assert.DoesNotContain(MigrationFixtures.SentinelPrefix, text, StringComparison.Ordinal);
            Assert.DoesNotContain("example.invalid", text, StringComparison.Ordinal);
            Assert.DoesNotContain(StorageRoot, text, StringComparison.Ordinal);
            Assert.DoesNotContain("candidates/", text, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task The_staging_schema_does_not_survive_a_successful_run()
    {
        await PrepareAsync();
        await RunAsync();

        await using var dbContext = NewContext();
        var schemas = await dbContext.Database
            .SqlQuery<string>($"SELECT schema_name AS \"Value\" FROM information_schema.schemata")
            .ToListAsync();
        Assert.DoesNotContain("migration_staging", schemas);
    }

    [Fact]
    public async Task Validate_writes_no_business_data_but_still_reports()
    {
        await PrepareAsync();

        var (exitCode, run, _, _) = await RunAsync(MigrationVerbs.Validate);

        Assert.Equal(0, exitCode);
        var report = await StoredReportAsync(run.Id);
        Assert.True(report.Reconciles);
        Assert.Null(report.PreMigrationBackup);

        await using var dbContext = NewContext();
        var leftBehind = await dbContext.Candidates.AsNoTracking()
            .Select(value => value.SourceKey ?? "(no source key)").ToListAsync();
        Assert.True(leftBehind.Count == 0, $"validate persisted: {string.Join(", ", leftBehind)}");
    }

    [Fact]
    public async Task Validate_reports_document_problems_before_anything_is_committed()
    {
        await PrepareAsync();

        var (_, run, _, _) = await RunAsync(MigrationVerbs.Validate);

        // Knowing about a bad hash or an infected CV is worth more before the load than
        // after it, and neither check needs the binary in private storage first.
        var report = await StoredReportAsync(run.Id);
        Assert.Contains(
            report.Documents,
            entry => entry.SourceKey == "D-006" && entry.ReasonCode == "document.hash.mismatch");
        Assert.Contains(
            report.Documents,
            entry => entry.SourceKey == "D-007" && entry.ReasonCode == "document.scan.rejected");
        Assert.Contains(report.Documents, entry => entry.SourceKey == "D-001" && entry.Result == "matched");

        // Verified, not moved.
        Assert.False(Directory.Exists(Path.Combine(StorageRoot, "available"))
            && Directory.EnumerateFiles(Path.Combine(StorageRoot, "available"), "*", SearchOption.AllDirectories).Any());
        await using var dbContext = NewContext();
        Assert.Equal(0, await dbContext.Documents.CountAsync());
    }

    [Fact]
    public async Task Report_re_emits_a_recorded_run_without_the_export_being_present()
    {
        await PrepareAsync();
        var (_, run, _, _) = await RunAsync();
        var reEmitDirectory = Path.Combine(_workspace, "re-emitted");

        var commandLine = new MigrationCommandLine
        {
            Verb = MigrationVerbs.Report,
            ConnectionString = database.ConnectionString,
            OutputDirectory = reEmitDirectory,
            RunId = run.Id,
        };
        var output = new StringWriter();
        var exitCode = await new MigrationRunner(
            commandLine,
            new FileSystemDocumentStorage(new DocumentStorageOptions { Root = StorageRoot }),
            new DocumentContentInspector(),
            new MarkerMalwareScanner(),
            DocumentStorageOptions.AbsoluteMaximumBytes,
            output,
            new StringWriter()).RunAsync(
            new MigrationRun(Guid.CreateVersion7(), MigrationVerbs.Report, null, DateTimeOffset.UtcNow),
            CancellationToken.None);

        Assert.Equal(0, exitCode);
        Assert.True(File.Exists(Path.Combine(reEmitDirectory, $"reconciliation-{run.Id}.json")));
        Assert.True(File.Exists(Path.Combine(reEmitDirectory, $"reconciliation-{run.Id}.md")));
    }

    [Fact]
    public async Task A_structurally_broken_export_writes_nothing_and_fails()
    {
        await PrepareAsync();
        var brokenExport = Path.Combine(_workspace, "broken");
        Directory.CreateDirectory(brokenExport);
        File.WriteAllText(Path.Combine(brokenExport, "candidates.csv"), "SourceKey\n");

        var commandLine = new MigrationCommandLine
        {
            Verb = MigrationVerbs.Load,
            ConnectionString = database.ConnectionString,
            ExportDirectory = brokenExport,
            OutputDirectory = OutputDirectory,
            PreMigrationBackup = "pre-ktl7-broken",
        };
        var run = new MigrationRun(Guid.CreateVersion7(), MigrationVerbs.Load, "pre-ktl7-broken", DateTimeOffset.UtcNow);
        await using (var dbContext = NewContext())
        {
            dbContext.MigrationRuns.Add(run);
            await dbContext.SaveChangesAsync();
        }

        var error = new StringWriter();
        var exitCode = await new MigrationRunner(
            commandLine,
            new FileSystemDocumentStorage(new DocumentStorageOptions { Root = StorageRoot }),
            new DocumentContentInspector(),
            new MarkerMalwareScanner(),
            DocumentStorageOptions.AbsoluteMaximumBytes,
            new StringWriter(),
            error).RunAsync(run, CancellationToken.None);

        Assert.Equal(3, exitCode);
        Assert.Contains("does not match the documented contract", error.ToString(), StringComparison.Ordinal);
        Assert.Contains("Nothing was written", error.ToString(), StringComparison.Ordinal);

        await using var read = NewContext();
        Assert.Equal(0, await read.Candidates.CountAsync());
        var recorded = await read.MigrationRuns.AsNoTracking().SingleAsync(value => value.Id == run.Id);
        Assert.Equal(MigrationOutcomes.Failed, recorded.Outcome);
    }
}
