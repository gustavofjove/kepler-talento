using KeplerTalento.Domain.Operations;
using KeplerTalento.Domain.Documents;
using KeplerTalento.Infrastructure.Documents;
using KeplerTalento.Infrastructure.Persistence;
using KeplerTalento.Tools.DataMigration;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace KeplerTalento.Tests.IntegrationTests;

/// <summary>
/// The documented rollback, exercised rather than described.
/// </summary>
/// <remarks>
/// The migration has no undo verb: rolling it back means restoring the backup taken before
/// it, which is what the runbook says and what the KTL-5 recovery scripts already do. This
/// drill performs a real <c>pg_dump</c> and restore inside the database container, so the
/// procedure is proven end to end and not merely written down.
/// </remarks>
public sealed class RollbackDrillTests(PostgreSqlFixture database)
    : IClassFixture<PostgreSqlFixture>, IDisposable
{
    private const string DumpPath = "/tmp/ktl7-pre-migration.dump";

    private readonly string _workspace =
        Path.Combine(Path.GetTempPath(), $"ktl-rollback-{Guid.NewGuid():N}");

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

    /// <summary>Takes the pre-migration backup, standing in for the KTL-5 backup script.</summary>
    private async Task SnapshotAsync()
    {
        var (exitCode, _, stderr) = await database.ExecAsync(
            "pg_dump", "-U", "postgres", "-d", PostgreSqlFixture.DatabaseName, "-Fc", "-f", DumpPath);
        Assert.True(exitCode == 0, stderr);
    }

    /// <summary>Restores it, standing in for the KTL-5 restore script.</summary>
    private async Task RestoreAsync()
    {
        // Npgsql's pooled connections would keep the old objects locked during the restore.
        NpgsqlConnection.ClearAllPools();
        var (exitCode, _, stderr) = await database.ExecAsync(
            "pg_restore", "-U", "postgres", "-d", PostgreSqlFixture.DatabaseName, "--clean", "--if-exists", DumpPath);
        Assert.True(exitCode == 0, stderr);
        NpgsqlConnection.ClearAllPools();
    }

    private async Task<int> RunLoadAsync()
    {
        var storageOptions = new DocumentStorageOptions { Root = Path.Combine(_workspace, "storage") };
        FileSystemDocumentStorage.ValidateAndPrepare(storageOptions);

        var commandLine = new MigrationCommandLine
        {
            Verb = MigrationVerbs.Load,
            ConnectionString = database.ConnectionString,
            ExportDirectory = MigrationFixtures.ExportDirectory,
            MappingFile = MigrationFixtures.MappingFile,
            OutputDirectory = Path.Combine(_workspace, "reports"),
            PreMigrationBackup = "ktl7-rollback-drill",
        };
        var run = new MigrationRun(
            Guid.CreateVersion7(), MigrationVerbs.Load, commandLine.PreMigrationBackup, DateTimeOffset.UtcNow);
        await using (var dbContext = NewContext())
        {
            dbContext.MigrationRuns.Add(run);
            await dbContext.SaveChangesAsync();
        }

        return await new MigrationRunner(
            commandLine,
            new FileSystemDocumentStorage(storageOptions),
            new DocumentContentInspector(),
            new MarkerMalwareScanner(),
            storageOptions.MaximumBytes,
            new StringWriter(),
            new StringWriter()).RunAsync(run, CancellationToken.None);
    }

    private async Task<(int Candidates, int Relations, int Documents, int Catalogs)> StateAsync()
    {
        await using var dbContext = NewContext();
        return (
            await dbContext.Candidates.CountAsync(),
            await dbContext.CandidateLanguages.CountAsync()
                + await dbContext.CandidatePrograms.CountAsync()
                + await dbContext.CandidateEducation.CountAsync()
                + await dbContext.CandidateExperience.CountAsync()
                + await dbContext.CandidateSkills.CountAsync(),
            await dbContext.Documents.CountAsync(),
            await dbContext.CatalogItems.CountAsync());
    }

    [Fact]
    public async Task Restoring_the_pre_migration_backup_returns_the_database_to_its_earlier_state()
    {
        await PrepareAsync();
        var before = await StateAsync();
        await SnapshotAsync();

        Assert.Equal(0, await RunLoadAsync());
        var afterLoad = await StateAsync();
        Assert.Equal(4, afterLoad.Candidates);
        Assert.True(afterLoad.Relations > 0);

        await RestoreAsync();

        var afterRestore = await StateAsync();
        Assert.Equal(before, afterRestore);
        // Catalog vocabulary predates the migration and comes back with everything else.
        Assert.Equal(before.Catalogs, afterRestore.Catalogs);
    }

    [Fact]
    public async Task A_migration_can_be_run_again_from_the_restored_state()
    {
        // The rollback is only worth anything if you can proceed from it.
        await PrepareAsync();
        await SnapshotAsync();
        Assert.Equal(0, await RunLoadAsync());
        await RestoreAsync();

        Assert.Equal(0, await RunLoadAsync());

        var afterSecondLoad = await StateAsync();
        Assert.Equal(4, afterSecondLoad.Candidates);
        // Two document rows: the clean one, and the infected one, which is recorded in an
        // unavailable state rather than not recorded at all.
        Assert.Equal(2, afterSecondLoad.Documents);

        await using var dbContext = NewContext();
        Assert.Equal(
            1,
            await dbContext.Documents.CountAsync(value => value.ScanState == DocumentScanState.Clean));
    }

    [Fact]
    public async Task Restoring_only_the_database_leaves_the_migrated_binaries_behind()
    {
        // This is why the recovery set covers the document volume as well as PostgreSQL:
        // pg_restore alone rolls back the metadata, and the binaries it pointed at survive
        // as orphans under keys nothing references any more.
        await PrepareAsync();
        await SnapshotAsync();
        await RunLoadAsync();

        var available = Path.Combine(_workspace, "storage", "available");
        var beforeRestore = Directory.EnumerateFiles(available, "*", SearchOption.AllDirectories).Count();
        Assert.True(beforeRestore > 0);

        await RestoreAsync();

        await using (var dbContext = NewContext())
        {
            Assert.Equal(0, await dbContext.Documents.CountAsync());
        }
        Assert.Equal(
            beforeRestore,
            Directory.EnumerateFiles(available, "*", SearchOption.AllDirectories).Count());
    }

    [Fact]
    public async Task The_report_names_the_recovery_set_that_undoes_the_run()
    {
        await PrepareAsync();
        await SnapshotAsync();
        await RunLoadAsync();

        await using var dbContext = NewContext();
        var run = await dbContext.MigrationRuns
            .AsNoTracking()
            .OrderByDescending(value => value.StartedAtUtc)
            .FirstAsync();

        // Whoever has to undo this reads the label from the artifact that recorded it.
        Assert.Equal("ktl7-rollback-drill", run.BackupLabel);
        Assert.Contains("ktl7-rollback-drill", run.ReportJson!, StringComparison.Ordinal);
    }
}
