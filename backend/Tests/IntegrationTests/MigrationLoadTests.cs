using KeplerTalento.Domain.Candidates;
using KeplerTalento.Infrastructure.Persistence;
using KeplerTalento.Tools.DataMigration.Export;
using KeplerTalento.Tools.DataMigration.Loading;
using KeplerTalento.Tools.DataMigration.Resolution;
using KeplerTalento.Tools.DataMigration.Validation;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace KeplerTalento.Tests.IntegrationTests;

/// <summary>
/// The load against a disposable PostgreSQL and the synthetic export set. Every assertion
/// here is about what ends up in the database, not about what the loader reports it did.
/// </summary>
public sealed class MigrationLoadTests(PostgreSqlFixture database) : IClassFixture<PostgreSqlFixture>
{
    private DbContextOptions<ApplicationDbContext> Options =>
        new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(database.ConnectionString).Options;

    private ApplicationDbContext NewContext() => new(Options);

    /// <summary>
    /// Starts from an empty candidate schema every time, so one test's rows cannot make
    /// another's counts pass.
    /// </summary>
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

    private async Task<MigrationLoader> LoaderAsync()
    {
        await using var dbContext = NewContext();
        var entries = await dbContext.CatalogItems
            .AsNoTracking()
            .Select(item => new CatalogResolver.CatalogEntry(item.Id, item.Family, item.Code, item.NameEs))
            .ToListAsync();
        Assert.True(MappingFile.TryRead(MigrationFixtures.MappingFile, out var mappings, out _));
        return new MigrationLoader(NewContext, new CatalogResolver(entries, mappings));
    }

    private static ExportSet Fixtures(string? root = null)
    {
        Assert.True(
            new ExportReader().TryRead(root ?? MigrationFixtures.ExportDirectory, out var exportSet, out var problems),
            string.Join("; ", problems));
        return exportSet;
    }

    private async Task<LoadResult> RunAsync(
        ExportSet? exportSet = null,
        bool overwriteApplicationEdits = false,
        DateTimeOffset? loadedAtUtc = null)
    {
        var loader = await LoaderAsync();
        return await loader.LoadAsync(
            exportSet ?? Fixtures(),
            new LoadOptions(overwriteApplicationEdits, loadedAtUtc ?? DateTimeOffset.UtcNow),
            CancellationToken.None);
    }

    [Fact]
    public async Task A_full_run_loads_the_clean_candidates_and_rejects_the_rest()
    {
        await PrepareAsync();

        var result = await RunAsync();

        Assert.Equal(
            ["C-001", "C-002", "C-006", "C-007"],
            result.SourceKeys(MigrationEntities.Candidate, RowOutcome.Loaded));
        Assert.Equal(
            ["C-003", "C-004", "C-005"],
            result.SourceKeys(MigrationEntities.Candidate, RowOutcome.Rejected));

        await using var read = NewContext();
        Assert.Equal(4, await read.Candidates.CountAsync());
    }

    [Fact]
    public async Task Every_source_row_is_accounted_for_as_loaded_rejected_or_skipped()
    {
        await PrepareAsync();
        var exportSet = Fixtures();

        var result = await RunAsync(exportSet);

        // Documents are loaded in the document phase, so they are not yet counted here.
        var accountedFor = result.Count(RowOutcome.Loaded)
            + result.Count(RowOutcome.Rejected)
            + result.Count(RowOutcome.Skipped);
        var sourceRowsExcludingDocuments = exportSet.TotalRows - exportSet.RowCount(ExportContract.Documents);
        Assert.Equal(sourceRowsExcludingDocuments, accountedFor);
    }

    [Fact]
    public async Task Consent_and_retention_metadata_round_trips_exactly()
    {
        await PrepareAsync();
        await RunAsync();

        await using var read = NewContext();
        var candidate = await read.Candidates.AsNoTracking().SingleAsync(value => value.SourceKey == "C-001");
        Assert.Equal(new DateOnly(2026, 1, 10), candidate.ReceivedAt);
        Assert.Equal(new DateOnly(2026, 1, 10), candidate.ConsentAt);
        Assert.Equal(new DateOnly(2028, 1, 10), candidate.ReviewDueAt);
    }

    [Fact]
    public async Task A_row_without_consent_metadata_is_rejected_rather_than_defaulted()
    {
        await PrepareAsync();

        var result = await RunAsync();

        Assert.Contains(
            result.Problems,
            problem => problem.SourceKey == "C-003" && problem.ReasonCode == ReasonCodes.ConsentMissing);
        await using var read = NewContext();
        Assert.False(await read.Candidates.AnyAsync(value => value.SourceKey == "C-003"));
    }

    [Fact]
    public async Task Logical_removal_in_the_source_arrives_as_logical_state()
    {
        await PrepareAsync();
        await RunAsync();

        await using var read = NewContext();
        var removed = await read.Candidates.AsNoTracking().SingleAsync(value => value.SourceKey == "C-002");
        Assert.False(removed.IsActive);
        Assert.NotNull(removed.DeletedAtUtc);
        Assert.Equal(new DateOnly(2026, 2, 1), DateOnly.FromDateTime(removed.DeletedAtUtc!.Value.UtcDateTime));
        // Counted as loaded, not skipped: the row made it across.
        Assert.Contains("C-002", await ReadKeysAsync());
    }

    private async Task<IReadOnlyList<string>> ReadKeysAsync()
    {
        await using var read = NewContext();
        return await read.Candidates.AsNoTracking()
            .Where(value => value.SourceKey != null)
            .Select(value => value.SourceKey!)
            .ToListAsync();
    }

    [Fact]
    public async Task Relations_are_loaded_against_their_candidate_and_resolve_to_catalog_entries()
    {
        await PrepareAsync();
        await RunAsync();

        await using var read = NewContext();
        var candidate = await read.Candidates.AsNoTracking().SingleAsync(value => value.SourceKey == "C-001");
        Assert.Equal(2, await read.CandidateLanguages.CountAsync(value => value.CandidateId == candidate.Id));
        Assert.Equal(1, await read.CandidatePrograms.CountAsync(value => value.CandidateId == candidate.Id));
        Assert.Equal(1, await read.CandidateEducation.CountAsync(value => value.CandidateId == candidate.Id));
        Assert.Equal(2, await read.CandidateExperience.CountAsync(value => value.CandidateId == candidate.Id));
        Assert.Equal(1, await read.CandidateSkills.CountAsync(value => value.CandidateId == candidate.Id));

        var language = await read.CandidateLanguages.AsNoTracking()
            .SingleAsync(value => value.SourceKey == "L-001");
        var english = await read.CatalogItems.AsNoTracking().SingleAsync(item => item.Code == "INGLES");
        Assert.Equal(english.Id, language.LanguageId);
    }

    [Fact]
    public async Task An_unresolvable_reference_rejects_its_candidate_rather_than_dropping_the_relation()
    {
        await PrepareAsync();

        var result = await RunAsync();

        Assert.Contains(
            result.Problems,
            problem => problem.SourceKey == "L-004" && problem.ReasonCode == ReasonCodes.ReferenceUnresolved);
        var unresolved = Assert.Single(result.UnresolvedValues);
        Assert.Equal("Klingon", unresolved.Value);

        await using var read = NewContext();
        // Neither the candidate nor a partial version of it.
        Assert.False(await read.Candidates.AnyAsync(value => value.SourceKey == "C-005"));
        Assert.False(await read.CandidateLanguages.AnyAsync(value => value.SourceKey == "L-004"));
    }

    [Fact]
    public async Task Re_running_the_same_export_produces_no_duplicates_and_unchanged_counts()
    {
        await PrepareAsync();
        var first = await RunAsync();
        var second = await RunAsync();

        Assert.Equal(
            first.SourceKeys(MigrationEntities.Candidate, RowOutcome.Loaded),
            second.SourceKeys(MigrationEntities.Candidate, RowOutcome.Loaded));

        await using var read = NewContext();
        Assert.Equal(4, await read.Candidates.CountAsync());
        Assert.Equal(5, await read.CandidateLanguages.CountAsync());
        Assert.Equal(3, await read.CandidatePrograms.CountAsync());
        Assert.Equal(3, await read.CandidateSkills.CountAsync());
    }

    [Fact]
    public async Task A_re_run_updates_in_place_and_keeps_the_same_identifier()
    {
        await PrepareAsync();
        await RunAsync();
        Guid identifier;
        await using (var read = NewContext())
        {
            identifier = (await read.Candidates.AsNoTracking().SingleAsync(value => value.SourceKey == "C-001")).Id;
        }

        await RunAsync();

        await using var after = NewContext();
        var reloaded = await after.Candidates.AsNoTracking().SingleAsync(value => value.SourceKey == "C-001");
        Assert.Equal(identifier, reloaded.Id);
    }

    [Fact]
    public async Task A_failure_partway_through_an_aggregate_leaves_none_of_it_behind()
    {
        await PrepareAsync();

        // A relation row already occupies the source key C-001's second language will claim,
        // so the aggregate fails after its candidate row has been written.
        Guid otherCandidateId;
        await using (var seed = NewContext())
        {
            var other = new Candidate(Guid.CreateVersion7(), "Otra", "Candidata", DateTimeOffset.UtcNow);
            other.SetSourceKey("C-999");
            seed.Candidates.Add(other);
            await seed.SaveChangesAsync();
            otherCandidateId = other.Id;

            var english = await seed.CatalogItems.SingleAsync(item => item.Code == "INGLES");
            var level = await seed.CatalogItems.SingleAsync(item => item.Code == "B1");
            var squatter = new CandidateLanguage(Guid.CreateVersion7(), otherCandidateId, english.Id, level.Id);
            squatter.SetSourceKey("L-002");
            seed.CandidateLanguages.Add(squatter);
            await seed.SaveChangesAsync();
        }

        var result = await RunAsync();

        Assert.Contains("C-001", result.SourceKeys(MigrationEntities.Candidate, RowOutcome.Rejected));
        Assert.Contains(
            result.Problems,
            problem => problem.SourceKey == "C-001" && problem.ReasonCode == ReasonCodes.LoadFailed);

        await using var read = NewContext();
        // Neither the candidate nor its first language, which had already been added.
        Assert.False(await read.Candidates.AnyAsync(value => value.SourceKey == "C-001"));
        Assert.False(await read.CandidateLanguages.AnyAsync(value => value.SourceKey == "L-001"));
        // The rest of the run is unaffected.
        Assert.True(await read.Candidates.AnyAsync(value => value.SourceKey == "C-002"));
    }

    [Fact]
    public async Task A_failure_reports_a_constraint_name_and_never_a_field_value()
    {
        await PrepareAsync();
        await using (var seed = NewContext())
        {
            var other = new Candidate(Guid.CreateVersion7(), "Otra", "Candidata", DateTimeOffset.UtcNow);
            other.SetSourceKey("C-999");
            seed.Candidates.Add(other);
            await seed.SaveChangesAsync();
            var english = await seed.CatalogItems.SingleAsync(item => item.Code == "INGLES");
            var level = await seed.CatalogItems.SingleAsync(item => item.Code == "B1");
            var squatter = new CandidateLanguage(Guid.CreateVersion7(), other.Id, english.Id, level.Id);
            squatter.SetSourceKey("L-002");
            seed.CandidateLanguages.Add(squatter);
            await seed.SaveChangesAsync();
        }

        var result = await RunAsync();

        var failure = Assert.Single(
            result.Problems,
            problem => problem.ReasonCode == ReasonCodes.LoadFailed);
        Assert.Equal("UX_CND_CandidateLanguages_SourceKey", failure.Field);
        Assert.All(
            result.Problems,
            problem => Assert.DoesNotContain(
                MigrationFixtures.SentinelPrefix,
                problem.ToString(),
                StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_record_the_application_created_is_never_touched_by_a_re_run()
    {
        await PrepareAsync();
        await RunAsync();

        var applicationCandidateId = Guid.CreateVersion7();
        await using (var write = NewContext())
        {
            // No source key: the application made this one.
            write.Candidates.Add(new Candidate(applicationCandidateId, "Alta", "Manual", DateTimeOffset.UtcNow));
            await write.SaveChangesAsync();
        }

        await RunAsync();

        await using var read = NewContext();
        Assert.True(await read.Candidates.AnyAsync(value => value.Id == applicationCandidateId));
        Assert.Equal(5, await read.Candidates.CountAsync());
    }
}
