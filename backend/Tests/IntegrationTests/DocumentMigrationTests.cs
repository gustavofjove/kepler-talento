using KeplerTalento.Application.Abstractions.Documents;
using KeplerTalento.Domain.Documents;
using KeplerTalento.Infrastructure.Documents;
using KeplerTalento.Infrastructure.Persistence;
using KeplerTalento.Tools.DataMigration.Export;
using KeplerTalento.Tools.DataMigration.Loading;
using KeplerTalento.Tools.DataMigration.Resolution;
using KeplerTalento.Tools.DataMigration.Validation;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace KeplerTalento.Tests.IntegrationTests;

/// <summary>
/// Document migration through the existing quarantine and scanning pipeline: migrated
/// content earns availability the same way an upload does, or it does not become available.
/// </summary>
public sealed class DocumentMigrationTests(PostgreSqlFixture database) : IClassFixture<PostgreSqlFixture>, IDisposable
{
    private readonly string _storageRoot =
        Path.Combine(Path.GetTempPath(), $"ktl-docs-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_storageRoot))
        {
            Directory.Delete(_storageRoot, recursive: true);
        }
    }

    private DbContextOptions<ApplicationDbContext> Options =>
        new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(database.ConnectionString).Options;

    private ApplicationDbContext NewContext() => new(Options);

    private FileSystemDocumentStorage NewStorage()
    {
        var options = new DocumentStorageOptions { Root = _storageRoot };
        FileSystemDocumentStorage.ValidateAndPrepare(options);
        return new FileSystemDocumentStorage(options);
    }

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

    private async Task<(LoadResult Result, FileSystemDocumentStorage Storage, MarkerMalwareScanner Scanner)>
        RunAsync(IMalwareScanner? scanner = null)
    {
        await PrepareAsync();
        var storage = NewStorage();
        var markerScanner = new MarkerMalwareScanner();

        await using var dbContext = NewContext();
        var entries = await dbContext.CatalogItems
            .AsNoTracking()
            .Select(item => new CatalogResolver.CatalogEntry(item.Id, item.Family, item.Code, item.NameEs))
            .ToListAsync();
        Assert.True(MappingFile.TryRead(MigrationFixtures.MappingFile, out var mappings, out _));
        Assert.True(new ExportReader().TryRead(MigrationFixtures.ExportDirectory, out var exportSet, out var problems),
            string.Join("; ", problems));

        var loader = new MigrationLoader(NewContext, new CatalogResolver(entries, mappings));
        var result = await loader.LoadAsync(
            exportSet,
            new LoadOptions(OverwriteApplicationEdits: false, DateTimeOffset.UtcNow),
            CancellationToken.None);

        var candidateIds = await loader.LoadedCandidateIdsAsync(result, CancellationToken.None);
        await new DocumentMigrator(
            NewContext,
            storage,
            new DocumentContentInspector(),
            scanner ?? markerScanner,
            DocumentStorageOptions.AbsoluteMaximumBytes)
            .MigrateAsync(exportSet, candidateIds, result, CancellationToken.None);

        return (result, storage, markerScanner);
    }

    [Fact]
    public async Task A_clean_document_reaches_available_storage_under_an_opaque_key()
    {
        var (result, storage, _) = await RunAsync();

        Assert.Contains("D-001", result.SourceKeys(MigrationEntities.Document, RowOutcome.Loaded));

        await using var read = NewContext();
        var document = await read.Documents.AsNoTracking().SingleAsync(value => value.SourceKey == "D-001");
        Assert.Equal(DocumentScanState.Clean, document.ScanState);
        Assert.True(await storage.AvailableExistsAsync(document.StorageKey, CancellationToken.None));

        // The key is application-generated and unrelated to the original filename.
        Assert.DoesNotContain("SENTINELFICHERO01", document.StorageKey, StringComparison.Ordinal);
        Assert.DoesNotContain("cv-001", document.StorageKey, StringComparison.Ordinal);
        Assert.StartsWith("candidates/", document.StorageKey, StringComparison.Ordinal);
        // The original filename survives as metadata only.
        Assert.Equal("SENTINELFICHERO01.txt", document.OriginalFileName);
    }

    [Fact]
    public async Task The_stored_hash_matches_the_bytes_that_were_actually_written()
    {
        var (_, storage, _) = await RunAsync();

        await using var read = NewContext();
        var document = await read.Documents.AsNoTracking().SingleAsync(value => value.SourceKey == "D-001");
        var onDisk = await storage.ComputeAvailableSha256Async(document.StorageKey, CancellationToken.None);

        Assert.Equal(document.Sha256, onDisk);
        Assert.Equal(
            await MigrationFixtures.Sha256Async(MigrationFixtures.DocumentFile("cv-001.txt"), CancellationToken.None),
            onDisk);
    }

    [Fact]
    public async Task A_document_whose_hash_disagrees_with_the_manifest_never_becomes_available()
    {
        var (result, storage, _) = await RunAsync();

        Assert.Contains(
            result.Problems,
            problem => problem.SourceKey == "D-006" && problem.ReasonCode == ReasonCodes.DocumentHashMismatch);

        await using var read = NewContext();
        // Rejected before a row was ever written: the binary and the manifest disagree, and
        // which of them is wrong is not the migration's to guess.
        Assert.False(await read.Documents.AnyAsync(value => value.SourceKey == "D-006"));
        Assert.Empty(Directory.EnumerateFiles(
            Path.Combine(_storageRoot, "quarantine"), "*", SearchOption.AllDirectories));
        _ = storage;
    }

    [Fact]
    public async Task An_infected_document_never_becomes_available_and_is_reported()
    {
        var (result, storage, _) = await RunAsync();

        Assert.Contains(
            result.Problems,
            problem => problem.SourceKey == "D-007" && problem.ReasonCode == ReasonCodes.DocumentRejectedByScanner);

        await using var read = NewContext();
        var document = await read.Documents.AsNoTracking().SingleAsync(value => value.SourceKey == "D-007");
        Assert.Equal(DocumentScanState.Infected, document.ScanState);
        Assert.False(await storage.AvailableExistsAsync(document.StorageKey, CancellationToken.None));
    }

    [Fact]
    public async Task An_unscannable_document_fails_closed()
    {
        var (result, storage, _) = await RunAsync(
            new FakeMalwareScanner(new ScanResult(ScanVerdict.Error, "scanner.timeout")));

        Assert.Contains(
            result.Problems,
            problem => problem.SourceKey == "D-001" && problem.ReasonCode == ReasonCodes.DocumentUnscannable);

        await using var read = NewContext();
        var document = await read.Documents.AsNoTracking().SingleAsync(value => value.SourceKey == "D-001");
        Assert.Equal(DocumentScanState.ScanFailed, document.ScanState);
        Assert.False(await storage.AvailableExistsAsync(document.StorageKey, CancellationToken.None));
    }

    [Fact]
    public async Task A_failing_document_does_not_reject_its_candidate()
    {
        var (result, _, _) = await RunAsync();

        // C-006 and C-007 own the two failing documents and are loaded regardless: a
        // document is content attached to a person, not a field of them.
        Assert.Contains("C-006", result.SourceKeys(MigrationEntities.Candidate, RowOutcome.Loaded));
        Assert.Contains("C-007", result.SourceKeys(MigrationEntities.Candidate, RowOutcome.Loaded));

        await using var read = NewContext();
        Assert.True(await read.Candidates.AnyAsync(value => value.SourceKey == "C-006"));
        Assert.True(await read.Candidates.AnyAsync(value => value.SourceKey == "C-007"));
    }

    [Fact]
    public async Task Every_document_is_scanned_and_none_is_trusted_for_being_migrated()
    {
        var (_, _, scanner) = await RunAsync();

        // Two files survive hashing and reach the scanner; the third is rejected on its hash
        // before scanning, which is a cheaper gate and a correct one.
        Assert.Equal(2, scanner.ScanCount);
    }

    [Fact]
    public async Task No_document_problem_carries_a_storage_key_a_path_or_a_sentinel()
    {
        var (result, _, _) = await RunAsync();

        var documentProblems = result.Problems
            .Where(problem => problem.Entity == MigrationEntities.Document)
            .ToList();
        Assert.NotEmpty(documentProblems);
        Assert.All(documentProblems, problem =>
        {
            var rendered = problem.ToString();
            Assert.DoesNotContain(MigrationFixtures.SentinelPrefix, rendered, StringComparison.Ordinal);
            Assert.DoesNotContain("candidates/", rendered, StringComparison.Ordinal);
            Assert.DoesNotContain(_storageRoot, rendered, StringComparison.Ordinal);
            Assert.DoesNotContain(":\\", rendered, StringComparison.Ordinal);
            Assert.DoesNotContain("cv-", rendered, StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task Document_metadata_carries_the_type_and_the_primary_flag()
    {
        await RunAsync();

        await using var read = NewContext();
        var document = await read.Documents.AsNoTracking().SingleAsync(value => value.SourceKey == "D-001");
        Assert.Equal("CV", document.DocumentType);
        Assert.True(document.IsPrimary);
    }
}
