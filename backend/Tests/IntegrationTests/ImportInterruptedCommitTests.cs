using System.Diagnostics;
using System.Net.Http.Json;
using System.Text;
using KeplerTalento.Application.Abstractions.Documents;
using KeplerTalento.Application.Abstractions.Identity;
using KeplerTalento.Domain.Candidates;
using KeplerTalento.Domain.Import;
using KeplerTalento.Infrastructure.Documents;
using KeplerTalento.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;
using Xunit.Abstractions;
using static KeplerTalento.Tests.IntegrationTests.ImportTestSupport;

namespace KeplerTalento.Tests.IntegrationTests;

/// <summary>
/// KTL-17 design D3, proven the hard way: the API process is killed partway through a commit —
/// not cancelled, not simulated — and restarted, and the resumed commit must yield exactly the
/// candidates a single uninterrupted commit would.
/// </summary>
/// <remarks>
/// A simulated interruption tests the simulation. This starts the real API executable as a child
/// process with its durable operation worker enabled, watches the row outcomes in PostgreSQL, and
/// kills the whole process tree once some but not all rows have been written. A second process is
/// then started against the same database and storage, and the test waits for the batch to finish.
/// </remarks>
[Collection(WebHostCollection.Name)]
public sealed class ImportInterruptedCommitTests(PostgreSqlFixture database, ITestOutputHelper output)
    : IClassFixture<PostgreSqlFixture>, IDisposable
{
    private const int Rows = 1500;
    private const int DuplicateEvery = 50;

    private readonly string _storageRoot = Path.Combine(Path.GetTempPath(), $"ktl-17-kill-{Guid.NewGuid():N}");

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("DocumentStorage__Root", null);
        if (Directory.Exists(_storageRoot))
        {
            Directory.Delete(_storageRoot, recursive: true);
        }
    }

    [Fact]
    public async Task A_commit_killed_mid_batch_resumes_after_restart_to_exactly_the_candidates_of_one_uninterrupted_run()
    {
        await using (var reset = NewDbContext())
        {
            await reset.Database.EnsureDeletedAsync();
            await DatabaseInitializer.MigrateAsync(reset, CancellationToken.None);
            await DatabaseInitializer.SeedCatalogsAsync(reset, CancellationToken.None);
            await ImportTestActor.SeedUserAsync(reset);
        }

        // Every fiftieth row repeats an earlier person, so the resumed run also has to reproduce
        // the duplicate decisions of the uninterrupted one, not only the loads.
        var lines = Enumerable.Range(1, Rows).Select(index =>
        {
            var person = index % DuplicateEvery == 0 ? index - 1 : index;
            return $"Nombre{person},Apellido{person},persona{person}@example.test,,,,Inglés:B2";
        }).ToArray();
        var expectedEmails = lines.Select(line => line.Split(',')[2]).Distinct().Order().ToArray();

        Guid batchId;
        await using (var factory = CreateFactory())
        {
            using var client = factory.CreateClient();
            var validated = await UploadAndValidateAsync(client, factory.Services, Csv(lines));
            Assert.Equal(ImportBatchStates.Validated, validated.State);
            Assert.Equal(expectedEmails.Length, validated.LoadedRows);
            using var commit = await CommitAsync(client, validated);
            commit.EnsureSuccessStatusCode();
            batchId = validated.Id;
        }

        // First process: let it write some rows, then kill it outright.
        var written = 0;
        using (var first = StartApi("first"))
        {
            var deadline = DateTime.UtcNow.AddMinutes(2);
            while (DateTime.UtcNow < deadline)
            {
                written = await CommitOutcomesAsync(batchId);
                if (written >= 50 || first.HasExited)
                {
                    break;
                }
                await Task.Delay(20);
            }
            Assert.False(first.HasExited, "The API process exited before it could be killed.");
            first.Kill(entireProcessTree: true);
            await first.WaitForExitAsync();
        }
        written = await CommitOutcomesAsync(batchId);
        output.WriteLine($"Killed after {written} of {Rows} rows.");
        Assert.InRange(written, 1, Rows - 1);
        await using (var interrupted = NewDbContext())
        {
            Assert.Equal(ImportBatchStates.Committing, (await interrupted.ImportBatches.SingleAsync()).State);
        }

        // Second process: the lease of the killed claim expires and the commit resumes.
        using (var second = StartApi("second"))
        {
            var deadline = DateTime.UtcNow.AddMinutes(4);
            string state;
            do
            {
                await Task.Delay(250);
                await using var poll = NewDbContext();
                state = (await poll.ImportBatches.AsNoTracking().SingleAsync()).State;
                Assert.False(second.HasExited, "The restarted API process exited.");
            }
            while (state == ImportBatchStates.Committing && DateTime.UtcNow < deadline);
            second.Kill(entireProcessTree: true);
            await second.WaitForExitAsync();
            Assert.Equal(ImportBatchStates.Committed, state);
        }

        await using var db = NewDbContext();
        var batch = await db.ImportBatches.AsNoTracking().SingleAsync();
        var candidates = await db.Candidates.AsNoTracking().Select(candidate => new { candidate.Id, candidate.Email }).ToListAsync();
        var outcomes = await db.ImportRowOutcomes.AsNoTracking().Where(outcome => outcome.Phase == ImportPhases.Commit).ToListAsync();

        Assert.Equal(expectedEmails, candidates.Select(candidate => candidate.Email).Order().ToArray());
        Assert.Equal(Rows, outcomes.Count);
        Assert.Equal(Enumerable.Range(1, Rows), outcomes.Select(outcome => outcome.RowNumber).Order());
        Assert.Equal(expectedEmails.Length, outcomes.Count(outcome => outcome.Outcome == ImportRowOutcomes.Loaded));
        Assert.Equal(Rows - expectedEmails.Length, outcomes.Count(outcome => outcome.Outcome == ImportRowOutcomes.Skipped));
        Assert.Equal(
            candidates.Select(candidate => candidate.Id).Order(),
            outcomes.Where(outcome => outcome.CandidateId is not null).Select(outcome => outcome.CandidateId!.Value).Order());
        Assert.Equal((batch.LoadedRows, batch.RejectedRows, batch.SkippedRows), (expectedEmails.Length, 0, Rows - expectedEmails.Length));
        // Candidate, relations and audit event were one transaction per row: none is orphaned.
        Assert.Equal(candidates.Count, await db.AuditEvents.CountAsync(audit => audit.EventType == CandidateAuditEvents.Created));
        Assert.Equal(candidates.Count, await db.CandidateLanguages.CountAsync());
    }

    private Task<int> CommitOutcomesAsync(Guid batchId) => WithDbAsync(db =>
        db.ImportRowOutcomes.CountAsync(outcome => outcome.BatchId == batchId && outcome.Phase == ImportPhases.Commit));

    private async Task<T> WithDbAsync<T>(Func<ApplicationDbContext, Task<T>> query)
    {
        await using var db = NewDbContext();
        return await query(db);
    }

    private Process StartApi(string label)
    {
        var webDll = LocateWebAssembly();
        var start = new ProcessStartInfo("dotnet", $"\"{webDll}\"")
        {
            WorkingDirectory = Path.GetDirectoryName(webDll)!,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        start.Environment["ASPNETCORE_ENVIRONMENT"] = "Testing";
        start.Environment["ASPNETCORE_URLS"] = "http://127.0.0.1:0";
        start.Environment["ConnectionStrings__ApplicationDatabase"] = database.ConnectionString;
        start.Environment["DevelopmentActor__Enabled"] = "true";
        start.Environment["DocumentStorage__Root"] = _storageRoot;
        start.Environment["OperationWorker__Enabled"] = "true";
        start.Environment["OperationWorker__PollSeconds"] = "1";
        start.Environment["OperationWorker__LeaseSeconds"] = "5";
        start.Environment["ProxyTrust__KnownNetworks__0"] = "127.0.0.0/8";
        start.Environment["Serilog__MinimumLevel"] = "Warning";
        var process = Process.Start(start) ?? throw new InvalidOperationException("Could not start the API process.");
        process.OutputDataReceived += (_, line) => { if (line.Data is not null) output.WriteLine($"[{label}] {line.Data}"); };
        process.ErrorDataReceived += (_, line) => { if (line.Data is not null) output.WriteLine($"[{label}:err] {line.Data}"); };
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        return process;
    }

    private static string LocateWebAssembly()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "KeplerTalento.slnx")))
        {
            current = current.Parent;
        }
        var configuration = AppContext.BaseDirectory.Contains($"{Path.DirectorySeparatorChar}Release{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
            ? "Release"
            : "Debug";
        var path = Path.Combine(current!.FullName, "Web", "bin", configuration, "net10.0", "Web.dll");
        Assert.True(File.Exists(path), $"Build the Web project first; {path} was not found.");
        return path;
    }

    private ApplicationDbContext NewDbContext() => new(
        new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(database.ConnectionString).Options);

    private WebApplicationFactory<Program> CreateFactory()
    {
        Environment.SetEnvironmentVariable("ConnectionStrings__ApplicationDatabase", database.ConnectionString);
        Environment.SetEnvironmentVariable("DocumentStorage__Root", _storageRoot);
        Environment.SetEnvironmentVariable("OperationWorker__Enabled", "false");
        Environment.SetEnvironmentVariable("DevelopmentActor__Enabled", "true");
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["DevelopmentActor:Enabled"] = "true",
                    ["ConnectionStrings:ApplicationDatabase"] = database.ConnectionString,
                    ["ProxyTrust:KnownNetworks:0"] = "127.0.0.0/8",
                }));
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<ICurrentActor>();
                services.AddScoped<ICurrentActor>(_ => ImportTestActor.Importer);
                services.RemoveAll<IMalwareScanner>();
                services.AddSingleton<IMalwareScanner>(new FakeMalwareScanner());
            });
        });
    }
}
