using System.Reflection;
using KeplerTalento.Domain.Operations;
using KeplerTalento.Infrastructure.Documents;
using KeplerTalento.Infrastructure.Persistence;
using KeplerTalento.Tools.DataMigration;
using KeplerTalento.Web.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace KeplerTalento.Tests.IntegrationTests;

/// <summary>
/// Security evidence for the slice, gathered from a real run rather than asserted.
/// </summary>
/// <remarks>
/// This change moves the whole candidate dataset, so the claims that matter are: no personal
/// data reaches any output, the bulk-data path is not reachable over HTTP, and the staging
/// copy of that data does not survive the run. Each is checked here against what the tool
/// actually produced.
/// </remarks>
public sealed class MigrationSecurityEvidenceTests(PostgreSqlFixture database)
    : IClassFixture<PostgreSqlFixture>, IDisposable
{
    private readonly string _workspace =
        Path.Combine(Path.GetTempPath(), $"ktl-evidence-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_workspace))
        {
            Directory.Delete(_workspace, recursive: true);
        }
    }

    private DbContextOptions<ApplicationDbContext> Options =>
        new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(database.ConnectionString).Options;

    private ApplicationDbContext NewContext() => new(Options);

    private async Task<(string Output, string Error, Guid RunId, string ReportsDirectory)> RunAsync()
    {
        await using (var dbContext = NewContext())
        {
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

        var reports = Path.Combine(_workspace, "reports");
        var storageOptions = new DocumentStorageOptions { Root = Path.Combine(_workspace, "storage") };
        FileSystemDocumentStorage.ValidateAndPrepare(storageOptions);

        var commandLine = new MigrationCommandLine
        {
            Verb = MigrationVerbs.Load,
            ConnectionString = database.ConnectionString,
            ExportDirectory = MigrationFixtures.ExportDirectory,
            MappingFile = MigrationFixtures.MappingFile,
            OutputDirectory = reports,
            PreMigrationBackup = "ktl7-evidence",
        };
        var run = new MigrationRun(
            Guid.CreateVersion7(), MigrationVerbs.Load, "ktl7-evidence", DateTimeOffset.UtcNow);
        await using (var dbContext = NewContext())
        {
            dbContext.MigrationRuns.Add(run);
            await dbContext.SaveChangesAsync();
        }

        var output = new StringWriter();
        var error = new StringWriter();
        await new MigrationRunner(
            commandLine,
            new FileSystemDocumentStorage(storageOptions),
            new DocumentContentInspector(),
            new MarkerMalwareScanner(),
            storageOptions.MaximumBytes,
            output,
            error).RunAsync(run, CancellationToken.None);

        return (output.ToString(), error.ToString(), run.Id, reports);
    }

    /// <summary>
    /// The values a leak would have to expose. The fixtures put a sentinel in every
    /// personal-data field precisely so one search can stand for all of them.
    /// </summary>
    private static IEnumerable<string> Forbidden() =>
    [
        MigrationFixtures.SentinelPrefix,
        "example.invalid",
        // An internal storage key.
        "candidates/",
        // Original filenames from the export, which are personal data by association.
        "cv-001",
        "cv-006",
        "cv-007",
    ];

    [Fact]
    public async Task No_personal_data_reaches_the_output_the_logs_or_either_report()
    {
        var (output, error, runId, reports) = await RunAsync();

        var json = await File.ReadAllTextAsync(Path.Combine(reports, $"reconciliation-{runId}.json"));
        var markdown = await File.ReadAllTextAsync(Path.Combine(reports, $"reconciliation-{runId}.md"));
        await using var dbContext = NewContext();
        var stored = await dbContext.MigrationRuns.AsNoTracking().SingleAsync(value => value.Id == runId);

        var surfaces = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["stdout"] = output,
            ["stderr"] = error,
            ["report.json"] = json,
            ["report.md"] = markdown,
            ["OPS_MigrationRuns.ReportJson"] = stored.ReportJson ?? string.Empty,
        };

        foreach (var (name, text) in surfaces)
        {
            foreach (var forbidden in Forbidden())
            {
                Assert.False(
                    text.Contains(forbidden, StringComparison.OrdinalIgnoreCase),
                    $"{name} contains '{forbidden}'");
            }
        }

        // The report itself must not embed the host layout it happens to be sitting in.
        // Standard output is the exception: it tells the operator where the report was
        // written, using the directory the operator supplied on the command line.
        foreach (var name in new[] { "report.json", "report.md", "OPS_MigrationRuns.ReportJson" })
        {
            Assert.False(
                surfaces[name].Contains(_workspace, StringComparison.OrdinalIgnoreCase),
                $"{name} contains the host workspace path");
        }
    }

    [Fact]
    public async Task Every_rejection_reason_is_exercised_so_the_leak_check_means_something()
    {
        // A search for sentinels proves nothing if the run produced no rejections to render.
        var (_, _, runId, _) = await RunAsync();

        await using var dbContext = NewContext();
        var stored = await dbContext.MigrationRuns.AsNoTracking().SingleAsync(value => value.Id == runId);
        var reportJson = stored.ReportJson!;

        foreach (var reasonCode in new[]
                 {
                     "consent.missing",
                     "email.invalid",
                     "reference.unresolved",
                     "document.hash.mismatch",
                     "document.scan.rejected",
                 })
        {
            Assert.Contains(reasonCode, reportJson, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task No_host_path_or_drive_letter_appears_in_any_output()
    {
        var (output, error, runId, reports) = await RunAsync();
        var markdown = await File.ReadAllTextAsync(Path.Combine(reports, $"reconciliation-{runId}.md"));
        await using var dbContext = NewContext();
        var stored = await dbContext.MigrationRuns.AsNoTracking().SingleAsync(value => value.Id == runId);

        // The tool prints where it wrote the report, which is a path the operator chose and
        // supplied. Nothing else may carry one, and nothing may carry a storage location.
        foreach (var text in new[] { error, markdown, stored.ReportJson! })
        {
            Assert.DoesNotContain(":\\", text, StringComparison.Ordinal);
            Assert.DoesNotContain("\\\\", text, StringComparison.Ordinal);
            Assert.DoesNotContain("/var/", text, StringComparison.Ordinal);
        }
        Assert.Contains("Report:", output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_staging_copy_of_the_dataset_does_not_survive_the_run()
    {
        await RunAsync();

        await using var connection = new NpgsqlConnection(database.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT count(*) FROM information_schema.schemata WHERE schema_name = 'migration_staging'",
            connection);
        Assert.Equal(0L, (long)(await command.ExecuteScalarAsync())!);
    }

    [Fact]
    public void The_migration_tool_exposes_no_http_surface()
    {
        var tool = typeof(MigrationRunner).Assembly;

        Assert.DoesNotContain(
            tool.GetReferencedAssemblies(),
            reference => reference.Name is not null
                && (reference.Name.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal)
                    || reference.Name.StartsWith("FastEndpoints", StringComparison.Ordinal)));
    }

    [Fact]
    public void The_api_cannot_reach_the_migration_tool()
    {
        // The whole point of shipping this as a command-line tool: if the API could
        // reference it, the bulk-data path would be one endpoint away from being reachable.
        var web = typeof(DevelopmentActor).Assembly;
        var toolName = typeof(MigrationRunner).Assembly.GetName().Name;

        Assert.DoesNotContain(
            web.GetReferencedAssemblies(),
            reference => string.Equals(reference.Name, toolName, StringComparison.Ordinal));
    }

    [Fact]
    public void No_endpoint_in_the_api_is_a_migration_endpoint()
    {
        var endpointTypes = typeof(DevelopmentActor).Assembly
            .GetTypes()
            .Where(type => type.Namespace?.StartsWith("KeplerTalento.Web.Features", StringComparison.Ordinal) == true)
            .Select(type => type.Name)
            .ToList();

        Assert.NotEmpty(endpointTypes);
        Assert.DoesNotContain(
            endpointTypes,
            name => name.Contains("Migrat", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Import", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Request_logging_records_no_candidate_field()
    {
        // The API's request-logging template is fixed and carries method, path, status and
        // elapsed time only, so candidate fields have no route into the log through it.
        var programSource = File.ReadAllText(
            Path.Combine(SolutionRoot(), "Web", "Program.cs"));

        Assert.Contains(
            "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms",
            programSource,
            StringComparison.Ordinal);
        foreach (var field in new[] { "FirstName", "LastName", "Email", "Phone", "Notes", "ConsentAt" })
        {
            Assert.DoesNotContain($"{{{field}}}", programSource, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void The_problem_type_has_no_place_to_put_a_field_value()
    {
        // The control that keeps values out of the report is structural: there is no
        // property for one. This fails if somebody adds a convenient "Value" alongside.
        var properties = typeof(KeplerTalento.Tools.DataMigration.Validation.RowProblem)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(property => property.Name)
            .ToList();

        Assert.Equal(["Entity", "SourceKey", "Field", "ReasonCode"], properties);
    }

    private static string SolutionRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "KeplerTalento.slnx")))
            {
                return current.FullName;
            }
            current = current.Parent;
        }
        throw new DirectoryNotFoundException("Could not locate the backend solution root.");
    }
}
