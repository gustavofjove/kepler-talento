using KeplerTalento.Infrastructure.Persistence;
using KeplerTalento.Application.Abstractions.Documents;
using KeplerTalento.Domain.Candidates;
using KeplerTalento.Domain.Documents;
using KeplerTalento.Domain.Operations;
using KeplerTalento.Infrastructure.Documents;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;
using KeplerTalento.Infrastructure.Operations;

namespace KeplerTalento.Tests.IntegrationTests;

public sealed class PostgreSqlPersistenceTests(PostgreSqlFixture database) : IClassFixture<PostgreSqlFixture>
{
    [Fact]
    public async Task Migration_seed_constraints_and_quoted_names_work_on_postgresql()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(database.ConnectionString)
            .Options;
        await using var dbContext = new ApplicationDbContext(options);
        await DatabaseInitializer.MigrateAsync(dbContext, CancellationToken.None);

        // There is no candidate seed to assert on: an empty database means an empty
        // candidate list. What this test is about is the physical naming convention and
        // the runtime role's grants, below.
        await using var connection = new NpgsqlConnection(database.ConnectionString);
        await connection.OpenAsync();
        await using var namesCommand = new NpgsqlCommand(
            "SELECT table_name FROM information_schema.tables WHERE table_schema='public' AND table_name LIKE ANY(ARRAY['CND\\_%','OPS\\_%','AUD\\_%','CAT\\_%']) ORDER BY table_name",
            connection);
        await using var reader = await namesCommand.ExecuteReaderAsync();
        var names = new List<string>();
        while (await reader.ReadAsync())
        {
            names.Add(reader.GetString(0));
        }
        Assert.Equal(
            [
                "AUD_Events",
                "CAT_CatalogItems",
                "CND_CandidateEducation",
                "CND_CandidateExperience",
                "CND_CandidateLanguages",
                "CND_CandidatePrograms",
                "CND_CandidateSkills",
                "CND_Candidates",
                "CND_Documents",
                "OPS_MigrationRuns",
                "OPS_Operations",
            ],
            names);
        await reader.CloseAsync();

        await using var grantsCommand = new NpgsqlCommand(
            "SELECT has_table_privilege('ktl_runtime', '\"CND_Candidates\"', 'SELECT'), has_table_privilege('ktl_runtime', '\"CND_Candidates\"', 'TRUNCATE'), has_table_privilege('ktl_runtime', '\"__EFMigrationsHistory\"', 'SELECT')",
            connection);
        await using var grantsReader = await grantsCommand.ExecuteReaderAsync();
        Assert.True(await grantsReader.ReadAsync());
        Assert.True(grantsReader.GetBoolean(0));
        Assert.False(grantsReader.GetBoolean(1));
        Assert.True(grantsReader.GetBoolean(2));
    }

    [Fact]
    public async Task Operation_claim_is_single_owner_recoverable_and_idempotent()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(database.ConnectionString).Options;
        await using (var setup = new ApplicationDbContext(options))
        {
            await DatabaseInitializer.MigrateAsync(setup, CancellationToken.None);
            var repository = new PostgreSqlOperationRepository(setup);
            await repository.EnqueueAsync("document.scan", "operation-test", $"document:{Guid.NewGuid()}:scan", CancellationToken.None);
        }

        await using var firstContext = new ApplicationDbContext(options);
        await using var secondContext = new ApplicationDbContext(options);
        var first = new PostgreSqlOperationRepository(firstContext);
        var second = new PostgreSqlOperationRepository(secondContext);
        var claims = await Task.WhenAll(
            first.ClaimNextAsync("worker-a", TimeSpan.FromSeconds(30), CancellationToken.None),
            second.ClaimNextAsync("worker-b", TimeSpan.FromSeconds(30), CancellationToken.None));
        var claimed = Assert.Single(claims, value => value is not null)!;
        var owner = claims[0] is not null ? "worker-a" : "worker-b";
        var repositoryForOwner = owner == "worker-a" ? first : second;
        Assert.True(await repositoryForOwner.RenewLeaseAsync(claimed.Id, owner, TimeSpan.FromSeconds(30), CancellationToken.None));
        Assert.True(await repositoryForOwner.CompleteAsync(claimed.Id, owner, "scanner.clean", CancellationToken.None));
        Assert.True(await repositoryForOwner.CompleteAsync(claimed.Id, owner, "scanner.clean", CancellationToken.None));
        Assert.Null(await repositoryForOwner.ClaimNextAsync(owner, TimeSpan.FromSeconds(30), CancellationToken.None));
    }

    [Fact]
    public async Task Scanner_outage_keeps_new_document_unavailable_but_existing_clean_download_works()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(database.ConnectionString).Options;
        var storageRoot = Path.Combine(Path.GetTempPath(), $"ktl-scanner-outage-{Guid.NewGuid():N}");
        try
        {
            var storageOptions = new DocumentStorageOptions { Root = storageRoot };
            FileSystemDocumentStorage.ValidateAndPrepare(storageOptions);
            var storage = new FileSystemDocumentStorage(storageOptions);
            var candidateId = Guid.NewGuid();
            var now = DateTimeOffset.UtcNow;
            var cleanId = Guid.NewGuid();
            var cleanKey = DocumentStorageKey.Create(candidateId, cleanId);
            var pendingId = Guid.NewGuid();
            var pendingKey = DocumentStorageKey.Create(candidateId, pendingId);
            var cleanBytes = "synthetic clean cv"u8.ToArray();
            var pendingBytes = "synthetic pending cv"u8.ToArray();
            var cleanStored = await storage.WriteQuarantineAsync(cleanKey, new MemoryStream(cleanBytes), 100, CancellationToken.None);
            await storage.PromoteAsync(cleanKey, CancellationToken.None);
            var pendingStored = await storage.WriteQuarantineAsync(pendingKey, new MemoryStream(pendingBytes), 100, CancellationToken.None);

            await using var dbContext = new ApplicationDbContext(options);
            await DatabaseInitializer.MigrateAsync(dbContext, CancellationToken.None);
            dbContext.Candidates.Add(new Candidate(candidateId, "Prueba", "Sintética", now));
            var cleanDocument = new CandidateDocument(cleanId, candidateId, cleanKey, "cv-limpio.pdf", "application/pdf", cleanStored.Size, cleanStored.Sha256, now);
            cleanDocument.MarkClean("previous-signature", now);
            var pendingDocument = new CandidateDocument(pendingId, candidateId, pendingKey, "cv-nuevo.pdf", "application/pdf", pendingStored.Size, pendingStored.Sha256, now);
            dbContext.Documents.AddRange(cleanDocument, pendingDocument);
            await dbContext.SaveChangesAsync();

            var unavailableScanner = new FakeMalwareScanner(new ScanResult(ScanVerdict.Error, "scanner.timeout"));
            var operation = new Operation(Guid.NewGuid(), "document.scan", "scanner-outage", $"document:{pendingId}:scan", now);
            var outcome = await new ScanOperationHandler(
                dbContext,
                storage,
                unavailableScanner,
                new DocumentRepository(dbContext)).HandleAsync(operation, CancellationToken.None);

            Assert.False(outcome.Completed);
            Assert.Equal("scanner.timeout", outcome.Code);
            Assert.Equal(DocumentScanState.ScanFailed, pendingDocument.ScanState);
            Assert.False(await storage.AvailableExistsAsync(pendingKey, CancellationToken.None));
            var cleanDownload = await new DocumentDownloadService(dbContext, storage).OpenCleanAsync(cleanId, CancellationToken.None);
            Assert.NotNull(cleanDownload);
            await cleanDownload!.Content.DisposeAsync();
        }
        finally
        {
            if (Directory.Exists(storageRoot)) Directory.Delete(storageRoot, recursive: true);
        }
    }

    [Fact]
    public async Task Download_refuses_every_non_available_state_and_a_missing_binary()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(database.ConnectionString).Options;
        var storageRoot = Path.Combine(Path.GetTempPath(), $"ktl-download-states-{Guid.NewGuid():N}");
        try
        {
            var storageOptions = new DocumentStorageOptions { Root = storageRoot };
            FileSystemDocumentStorage.ValidateAndPrepare(storageOptions);
            var storage = new FileSystemDocumentStorage(storageOptions);
            await using var dbContext = new ApplicationDbContext(options);
            await DatabaseInitializer.MigrateAsync(dbContext, CancellationToken.None);
            var candidateId = Guid.NewGuid();
            var now = DateTimeOffset.UtcNow;
            dbContext.Candidates.Add(new Candidate(candidateId, "Estados", "Documento", now));

            CandidateDocument Create(DocumentScanState state)
            {
                var id = Guid.NewGuid();
                var value = new CandidateDocument(
                    id,
                    candidateId,
                    DocumentStorageKey.Create(candidateId, id),
                    $"{state}.pdf",
                    "application/pdf",
                    10,
                    new string('a', 64),
                    now);
                if (state == DocumentScanState.Clean) value.MarkClean(null, now);
                else if (state != DocumentScanState.PendingScan) value.MarkUnavailable(state, $"document.{state}", now);
                return value;
            }

            var refused = new[]
            {
                Create(DocumentScanState.PendingScan),
                Create(DocumentScanState.Infected),
                Create(DocumentScanState.Rejected),
                Create(DocumentScanState.ScanFailed),
                Create(DocumentScanState.Clean),
            };
            dbContext.Documents.AddRange(refused);
            await dbContext.SaveChangesAsync();
            var downloads = new DocumentDownloadService(dbContext, storage);

            foreach (var document in refused)
            {
                Assert.Null(await downloads.OpenCleanAsync(document.Id, CancellationToken.None));
            }
        }
        finally
        {
            if (Directory.Exists(storageRoot)) Directory.Delete(storageRoot, recursive: true);
        }
    }

    [Fact]
    public async Task Expired_work_is_reclaimed_once_then_fails_at_the_retry_limit()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(database.ConnectionString).Options;
        Guid operationId;
        await using (var setup = new ApplicationDbContext(options))
        {
            await DatabaseInitializer.MigrateAsync(setup, CancellationToken.None);
            var operation = await new PostgreSqlOperationRepository(setup).EnqueueAsync(
                "document.scan",
                "retry-recovery",
                $"retry:{Guid.NewGuid():N}",
                CancellationToken.None);
            operationId = operation.Id;
            await setup.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE \"OPS_Operations\" SET \"Status\" = 'Running', \"Owner\" = 'dead-worker', \"AttemptCount\" = 2, \"LeaseExpiresAtUtc\" = {DateTimeOffset.UtcNow.AddMinutes(-1)} WHERE \"Id\" = {operationId}");
        }

        await using (var recoveryContext = new ApplicationDbContext(options))
        {
            var recovered = await new PostgreSqlOperationRepository(recoveryContext).ClaimNextAsync(
                "recovery-worker",
                TimeSpan.FromMilliseconds(1),
                CancellationToken.None);
            Assert.NotNull(recovered);
            Assert.Equal(operationId, recovered!.Id);
            Assert.Equal(3, recovered.AttemptCount);
        }

        await Task.Delay(10);
        await using var exhaustedContext = new ApplicationDbContext(options);
        var exhaustedRepository = new PostgreSqlOperationRepository(exhaustedContext);
        Assert.Null(await exhaustedRepository.ClaimNextAsync("next-worker", TimeSpan.FromSeconds(1), CancellationToken.None));
        var exhausted = await exhaustedRepository.FindAsync(operationId, CancellationToken.None);
        Assert.NotNull(exhausted);
        Assert.Equal(OperationStatus.Failed, exhausted!.Status);
        Assert.Equal("operation.retry.exhausted", exhausted.OutcomeCode);
    }

    [Fact]
    public async Task Operations_are_queryable_by_exact_correlation_identifier()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(database.ConnectionString).Options;
        await using var dbContext = new ApplicationDbContext(options);
        await DatabaseInitializer.MigrateAsync(dbContext, CancellationToken.None);
        var repository = new PostgreSqlOperationRepository(dbContext);
        var correlationId = $"correlation-{Guid.NewGuid():N}";
        var first = await repository.EnqueueAsync("document.scan", correlationId, $"operation:{Guid.NewGuid():N}", CancellationToken.None);
        var second = await repository.EnqueueAsync("document.scan", correlationId, $"operation:{Guid.NewGuid():N}", CancellationToken.None);
        var unrelated = await repository.EnqueueAsync("document.scan", $"other-{Guid.NewGuid():N}", $"operation:{Guid.NewGuid():N}", CancellationToken.None);

        var matches = await repository.FindByCorrelationIdAsync(correlationId, CancellationToken.None);

        Assert.Equal(2, matches.Count);
        Assert.Equal(new[] { first.Id, second.Id }.Order().ToArray(), matches.Select(value => value.Id).Order().ToArray());
        Assert.All(matches, value => Assert.Equal(correlationId, value.CorrelationId));
        Assert.True(await repository.CancelAsync(first.Id, null, "test.cleanup", CancellationToken.None));
        Assert.True(await repository.CancelAsync(second.Id, null, "test.cleanup", CancellationToken.None));
        Assert.True(await repository.CancelAsync(unrelated.Id, null, "test.cleanup", CancellationToken.None));
    }

    [Fact]
    public async Task Reconciliation_reports_missing_orphaned_and_stale_objects_with_redacted_audits()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(database.ConnectionString).Options;
        var storageRoot = Path.Combine(Path.GetTempPath(), $"ktl-reconciliation-{Guid.NewGuid():N}");
        try
        {
            var storageOptions = new DocumentStorageOptions { Root = storageRoot };
            FileSystemDocumentStorage.ValidateAndPrepare(storageOptions);
            var storage = new FileSystemDocumentStorage(storageOptions);
            var now = DateTimeOffset.UtcNow;
            var candidateId = Guid.NewGuid();
            var missingDocumentId = Guid.NewGuid();
            var missingKey = DocumentStorageKey.Create(candidateId, missingDocumentId);
            var staleDocumentId = Guid.NewGuid();
            var staleKey = DocumentStorageKey.Create(candidateId, staleDocumentId);
            var staleBytes = "synthetic stale quarantine"u8.ToArray();
            var staleStored = await storage.WriteQuarantineAsync(staleKey, new MemoryStream(staleBytes), 100, CancellationToken.None);
            File.SetLastWriteTimeUtc(
                DocumentStorageKey.ResolveContained(Path.Combine(storageRoot, storageOptions.QuarantineDirectory), staleKey),
                now.AddDays(-2).UtcDateTime);
            var orphanKey = DocumentStorageKey.Create(Guid.NewGuid(), Guid.NewGuid());
            await storage.WriteQuarantineAsync(orphanKey, new MemoryStream("synthetic orphan"u8.ToArray()), 100, CancellationToken.None);
            await storage.PromoteAsync(orphanKey, CancellationToken.None);

            await using var dbContext = new ApplicationDbContext(options);
            await DatabaseInitializer.MigrateAsync(dbContext, CancellationToken.None);
            await dbContext.AuditEvents.ExecuteDeleteAsync();
            await dbContext.Documents.ExecuteDeleteAsync();
            await dbContext.Candidates.ExecuteDeleteAsync();
            dbContext.Candidates.Add(new Candidate(candidateId, "Prueba", "ReconciliaciÃ³n", now));
            var missing = new CandidateDocument(
                missingDocumentId,
                candidateId,
                missingKey,
                "missing.pdf",
                "application/pdf",
                1,
                new string('a', 64),
                now);
            missing.MarkClean("synthetic-signature", now);
            dbContext.Documents.Add(missing);
            dbContext.Documents.Add(new CandidateDocument(
                staleDocumentId,
                candidateId,
                staleKey,
                "stale.txt",
                "text/plain",
                staleStored.Size,
                staleStored.Sha256,
                now.AddDays(-2)));
            await dbContext.SaveChangesAsync();

            var report = await new DocumentStorageReconciler(dbContext, storage)
                .ReconcileAsync(TimeSpan.FromHours(24), CancellationToken.None);

            Assert.False(report.IsSuccessful);
            Assert.Equal(2, report.DocumentsChecked);
            Assert.Equal(1, report.MissingObjects);
            Assert.Equal(0, report.HashMismatches);
            Assert.Equal(1, report.OrphanObjects);
            Assert.Equal(1, report.StaleQuarantineObjects);
            var audits = await dbContext.AuditEvents.AsNoTracking().ToListAsync();
            Assert.Contains(audits, value => value.SubjectId == missingDocumentId.ToString("N") && value.OutcomeCode == "document.reconciliation.missing");
            Assert.Contains(audits, value => value.SubjectId == staleDocumentId.ToString("N") && value.OutcomeCode == "document.reconciliation.stale_quarantine");
            var orphanAudit = Assert.Single(audits, value => value.OutcomeCode == "document.reconciliation.orphan");
            Assert.StartsWith("orphan-", orphanAudit.SubjectId, StringComparison.Ordinal);
            Assert.DoesNotContain(orphanKey, orphanAudit.SubjectId, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(storageRoot)) Directory.Delete(storageRoot, recursive: true);
        }
    }
}
