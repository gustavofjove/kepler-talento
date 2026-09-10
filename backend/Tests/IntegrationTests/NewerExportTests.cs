using KeplerTalento.Infrastructure.Persistence;
using KeplerTalento.Tools.DataMigration.Export;
using KeplerTalento.Tools.DataMigration.Loading;
using KeplerTalento.Tools.DataMigration.Resolution;
using KeplerTalento.Tools.DataMigration.Validation;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace KeplerTalento.Tests.IntegrationTests;

/// <summary>
/// What happens when a second, newer export arrives after the first has been loaded.
/// </summary>
/// <remarks>
/// This is the realistic path, not an edge case: HR keeps using Access between this change
/// and the KTL-8 cutover, so the migration is expected to run again over newer data. The
/// hazard these tests pin down is a re-run undoing work done in the application, which
/// idempotency alone does not prevent.
/// </remarks>
public sealed class NewerExportTests(PostgreSqlFixture database) : IClassFixture<PostgreSqlFixture>, IDisposable
{
    private readonly List<string> _temporaryDirectories = [];

    public void Dispose()
    {
        foreach (var directory in _temporaryDirectories.Where(Directory.Exists))
        {
            Directory.Delete(directory, recursive: true);
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
    }

    private async Task<LoadResult> RunAsync(
        string? exportRoot = null,
        bool overwriteApplicationEdits = false,
        DateTimeOffset? loadedAtUtc = null)
    {
        await using var dbContext = NewContext();
        var entries = await dbContext.CatalogItems
            .AsNoTracking()
            .Select(item => new CatalogResolver.CatalogEntry(item.Id, item.Family, item.Code, item.NameEs))
            .ToListAsync();
        Assert.True(MappingFile.TryRead(MigrationFixtures.MappingFile, out var mappings, out _));
        Assert.True(
            new ExportReader().TryRead(exportRoot ?? MigrationFixtures.ExportDirectory, out var exportSet, out var problems),
            string.Join("; ", problems));

        return await new MigrationLoader(NewContext, new CatalogResolver(entries, mappings)).LoadAsync(
            exportSet,
            new LoadOptions(overwriteApplicationEdits, loadedAtUtc ?? DateTimeOffset.UtcNow),
            CancellationToken.None);
    }

    /// <summary>
    /// Copies the fixture export and applies a transformation to one file, standing in for
    /// a newer extract from Access.
    /// </summary>
    private string NewerExport(string file, Func<string, string> transform)
    {
        var root = Path.Combine(Path.GetTempPath(), $"ktl-newer-{Guid.NewGuid():N}");
        _temporaryDirectories.Add(root);
        foreach (var source in Directory.EnumerateFiles(MigrationFixtures.ExportDirectory, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(MigrationFixtures.ExportDirectory, source);
            var destination = Path.Combine(root, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(source, destination);
        }
        var target = Path.Combine(root, file);
        File.WriteAllText(target, transform(File.ReadAllText(target)));
        return root;
    }

    private async Task EditInTheApplicationAsync(string sourceKey, DateTimeOffset editedAtUtc)
    {
        await using var dbContext = NewContext();
        var candidate = await dbContext.Candidates.SingleAsync(value => value.SourceKey == sourceKey);
        candidate.SetIdentity("Editada", "EnLaAplicación", editedAtUtc);
        await dbContext.SaveChangesAsync();
    }

    [Fact]
    public async Task A_record_edited_in_the_application_is_skipped_and_reported()
    {
        await PrepareAsync();
        var loadedAt = DateTimeOffset.UtcNow;
        await RunAsync(loadedAtUtc: loadedAt);
        await EditInTheApplicationAsync("C-001", loadedAt.AddHours(1));

        var result = await RunAsync(loadedAtUtc: loadedAt.AddHours(2));

        Assert.Contains("C-001", result.SourceKeys(MigrationEntities.Candidate, RowOutcome.Skipped));
        Assert.Contains(
            result.Problems,
            problem => problem.SourceKey == "C-001" && problem.ReasonCode == ReasonCodes.ApplicationChanged);
        Assert.Equal(0, result.OverwrittenApplicationEdits);

        await using var read = NewContext();
        var candidate = await read.Candidates.AsNoTracking().SingleAsync(value => value.SourceKey == "C-001");
        Assert.Equal("Editada", candidate.FirstName);
    }

    [Fact]
    public async Task Records_the_application_did_not_touch_still_update_without_the_override()
    {
        await PrepareAsync();
        var loadedAt = DateTimeOffset.UtcNow;
        await RunAsync(loadedAtUtc: loadedAt);
        await EditInTheApplicationAsync("C-001", loadedAt.AddHours(1));

        var newer = NewerExport(
            ExportContract.Candidates,
            text => text.Replace("SENTINELCIUDAD06", "CIUDADNUEVA06", StringComparison.Ordinal));
        var result = await RunAsync(newer, loadedAtUtc: loadedAt.AddHours(2));

        // C-001 is held back; C-006 is not, and picks up the newer value.
        Assert.Contains("C-001", result.SourceKeys(MigrationEntities.Candidate, RowOutcome.Skipped));
        Assert.Contains("C-006", result.SourceKeys(MigrationEntities.Candidate, RowOutcome.Loaded));

        await using var read = NewContext();
        var updated = await read.Candidates.AsNoTracking().SingleAsync(value => value.SourceKey == "C-006");
        Assert.Equal("CIUDADNUEVA06", updated.Location);
    }

    [Fact]
    public async Task The_override_overwrites_the_application_edit_and_says_how_many()
    {
        await PrepareAsync();
        var loadedAt = DateTimeOffset.UtcNow;
        await RunAsync(loadedAtUtc: loadedAt);
        await EditInTheApplicationAsync("C-001", loadedAt.AddHours(1));

        var result = await RunAsync(overwriteApplicationEdits: true, loadedAtUtc: loadedAt.AddHours(2));

        Assert.Contains("C-001", result.SourceKeys(MigrationEntities.Candidate, RowOutcome.Loaded));
        Assert.Equal(1, result.OverwrittenApplicationEdits);

        await using var read = NewContext();
        var candidate = await read.Candidates.AsNoTracking().SingleAsync(value => value.SourceKey == "C-001");
        Assert.Equal("SENTINELNOMBRE01", candidate.FirstName);
    }

    [Fact]
    public async Task A_freshly_loaded_record_reports_no_application_changes()
    {
        await PrepareAsync();
        var loadedAt = DateTimeOffset.UtcNow;

        await RunAsync(loadedAtUtc: loadedAt);

        await using var read = NewContext();
        var candidate = await read.Candidates.AsNoTracking().SingleAsync(value => value.SourceKey == "C-001");
        Assert.False(candidate.HasApplicationChangesSinceLoad);
        // The guard rests on these two agreeing, not on either matching the caller's clock:
        // PostgreSQL stores microseconds where DateTimeOffset carries 100-nanosecond ticks,
        // and both columns truncate identically because both are written from one value.
        Assert.NotNull(candidate.SourceLoadedAtUtc);
        Assert.Equal(candidate.UpdatedAtUtc, candidate.SourceLoadedAtUtc);
        Assert.True(
            (candidate.SourceLoadedAtUtc!.Value - loadedAt).Duration() < TimeSpan.FromMilliseconds(1),
            "the load moment should be the instant the run supplied");
    }

    [Fact]
    public async Task A_record_whose_source_key_vanished_from_the_export_is_reported_and_left_alone()
    {
        await PrepareAsync();
        await RunAsync();

        // Stands in for a candidate deleted in Access since the previous export.
        var newer = NewerExport(
            ExportContract.Candidates,
            text => string.Join(
                '\n',
                text.Split('\n').Where(line => !line.StartsWith("C-001,", StringComparison.Ordinal))));

        var result = await RunAsync(newer);

        var unmatched = Assert.Single(result.UnmatchedTargetRecords);
        Assert.Equal("C-001", unmatched.SourceKey);
        Assert.Equal(MigrationEntities.Candidate, unmatched.Entity);

        await using var read = NewContext();
        var stillThere = await read.Candidates.AsNoTracking().SingleAsync(value => value.SourceKey == "C-001");
        // Reported, never deactivated: an export query missing a WHERE clause looks the same
        // as a deletion, and deactivating live candidates on that is not worth automating.
        Assert.True(stillThere.IsActive);
    }

    [Fact]
    public async Task Unmatched_records_are_counted_apart_from_rejections_and_skips()
    {
        await PrepareAsync();
        await RunAsync();
        var newer = NewerExport(
            ExportContract.Candidates,
            text => string.Join(
                '\n',
                text.Split('\n').Where(line => !line.StartsWith("C-001,", StringComparison.Ordinal))));

        var result = await RunAsync(newer);
        var counts = result.ToCounts(result.TotalRows);

        Assert.Equal(1, counts.UnmatchedTargetRecords);
        Assert.DoesNotContain("C-001", result.SourceKeys(MigrationEntities.Candidate, RowOutcome.Rejected));
        Assert.DoesNotContain("C-001", result.SourceKeys(MigrationEntities.Candidate, RowOutcome.Skipped));
    }

    [Fact]
    public async Task A_newer_export_adds_rows_that_were_not_there_before()
    {
        await PrepareAsync();
        await RunAsync();

        var newer = NewerExport(
            ExportContract.Candidates,
            text => text.TrimEnd('\n') + "\n"
                + "C-008,NUEVONOMBRE08,NUEVOAPELLIDO08,+34 600 000 008,ocho@example.invalid,"
                + "CIUDAD08,PROVINCIA08,España,Inmediata,new,Email,NOTAS08,"
                + "2026-03-01,2026-03-01,2028-03-01,true,\n");

        var result = await RunAsync(newer);

        Assert.Contains("C-008", result.SourceKeys(MigrationEntities.Candidate, RowOutcome.Loaded));
        await using var read = NewContext();
        Assert.Equal(5, await read.Candidates.CountAsync());
    }
}
