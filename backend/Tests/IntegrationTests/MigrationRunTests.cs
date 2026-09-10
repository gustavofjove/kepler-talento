using KeplerTalento.Domain.Operations;
using KeplerTalento.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace KeplerTalento.Tests.IntegrationTests;

public sealed class MigrationRunTests(PostgreSqlFixture database) : IClassFixture<PostgreSqlFixture>
{
    private DbContextOptions<ApplicationDbContext> Options =>
        new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(database.ConnectionString).Options;

    private async Task<ApplicationDbContext> PrepareAsync()
    {
        var dbContext = new ApplicationDbContext(Options);
        await DatabaseInitializer.MigrateAsync(dbContext, CancellationToken.None);
        return dbContext;
    }

    [Fact]
    public async Task A_run_is_recorded_before_it_does_anything_and_finishes_with_its_counts()
    {
        await using var dbContext = await PrepareAsync();
        var startedAtUtc = DateTimeOffset.UtcNow;
        var run = new MigrationRun(Guid.CreateVersion7(), MigrationVerbs.Load, "pre-ktl7-backup", startedAtUtc);
        dbContext.MigrationRuns.Add(run);
        await dbContext.SaveChangesAsync();

        Assert.Equal(MigrationOutcomes.Running, run.Outcome);
        Assert.Null(run.FinishedAtUtc);

        var counts = new MigrationRunCounts(
            SourceRows: 10,
            Loaded: 7,
            Rejected: 2,
            Skipped: 1,
            UnmatchedTargetRecords: 3,
            UnresolvedValues: 0,
            DocumentsLoaded: 4,
            DocumentsRejected: 1);
        Assert.True(counts.Reconciles);
        run.Finish(MigrationOutcomes.Reconciled, counts, "{\"runId\":\"x\"}", startedAtUtc.AddMinutes(3));
        await dbContext.SaveChangesAsync();

        await using var read = new ApplicationDbContext(Options);
        var stored = await read.MigrationRuns.AsNoTracking().SingleAsync(value => value.Id == run.Id);
        Assert.Equal(MigrationOutcomes.Reconciled, stored.Outcome);
        Assert.Equal("pre-ktl7-backup", stored.BackupLabel);
        Assert.Equal(10, stored.SourceRows);
        Assert.Equal(3, stored.UnmatchedTargetRecords);
        Assert.Equal("{\"runId\":\"x\"}", stored.ReportJson);
        Assert.NotNull(stored.FinishedAtUtc);
    }

    [Fact]
    public async Task Counts_that_do_not_account_for_every_source_row_do_not_reconcile()
    {
        await using var _ = await PrepareAsync();
        var counts = new MigrationRunCounts(10, Loaded: 7, Rejected: 1, Skipped: 1, 0, 0, 0, 0);
        Assert.False(counts.Reconciles);
    }

    [Fact]
    public async Task A_load_cannot_be_recorded_without_the_backup_that_undoes_it()
    {
        await using var dbContext = await PrepareAsync();

        // The command line refuses first; this is the database holding the same line for any
        // writer that reaches the table another way.
        var failure = await Assert.ThrowsAsync<PostgresException>(() =>
            dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"""
                 INSERT INTO "OPS_MigrationRuns"
                     ("Id", "Verb", "Outcome", "BackupLabel", "StartedAtUtc", "SourceRows",
                      "LoadedRows", "RejectedRows", "SkippedRows", "UnmatchedTargetRecords",
                      "UnresolvedValues", "DocumentsLoaded", "DocumentsRejected")
                 VALUES ({Guid.NewGuid()}, 'load', 'running', NULL, now(), 0, 0, 0, 0, 0, 0, 0, 0)
                 """));
        Assert.Equal("CK_OPS_MigrationRuns_BackupLabel", failure.ConstraintName);
    }

    [Fact]
    public async Task Validate_needs_no_backup_label()
    {
        await using var dbContext = await PrepareAsync();
        dbContext.MigrationRuns.Add(new MigrationRun(
            Guid.CreateVersion7(),
            MigrationVerbs.Validate,
            backupLabel: null,
            DateTimeOffset.UtcNow));
        await dbContext.SaveChangesAsync();
    }

    [Fact]
    public async Task An_unknown_verb_or_outcome_is_rejected_by_the_database()
    {
        await using var dbContext = await PrepareAsync();
        var failure = await Assert.ThrowsAsync<PostgresException>(() =>
            dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"""
                 INSERT INTO "OPS_MigrationRuns"
                     ("Id", "Verb", "Outcome", "BackupLabel", "StartedAtUtc", "SourceRows",
                      "LoadedRows", "RejectedRows", "SkippedRows", "UnmatchedTargetRecords",
                      "UnresolvedValues", "DocumentsLoaded", "DocumentsRejected")
                 VALUES ({Guid.NewGuid()}, 'sync', 'running', NULL, now(), 0, 0, 0, 0, 0, 0, 0, 0)
                 """));
        Assert.Equal("CK_OPS_MigrationRuns_Verb", failure.ConstraintName);
    }

    [Fact]
    public async Task The_runtime_role_has_no_access_to_migration_history()
    {
        await using var _ = await PrepareAsync();
        await using var connection = new NpgsqlConnection(database.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            """
            SELECT has_table_privilege('ktl_runtime', '"OPS_MigrationRuns"', 'SELECT'),
                   has_table_privilege('ktl_runtime', '"OPS_MigrationRuns"', 'INSERT')
            """,
            connection);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.False(reader.GetBoolean(0));
        Assert.False(reader.GetBoolean(1));
    }
}
