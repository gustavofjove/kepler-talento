using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using KeplerTalento.Application.Abstractions.Documents;
using KeplerTalento.Application.Abstractions.Identity;
using KeplerTalento.Application.Abstractions.Import;
using KeplerTalento.Application.Features.Candidates;
using KeplerTalento.Application.Features.Import;
using KeplerTalento.Domain.Candidates;
using KeplerTalento.Domain.Identity;
using KeplerTalento.Domain.Import;
using KeplerTalento.Infrastructure.Documents;
using KeplerTalento.Infrastructure.Import;
using KeplerTalento.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Npgsql;
using Xunit;
using static KeplerTalento.Tests.IntegrationTests.ImportTestSupport;

namespace KeplerTalento.Tests.IntegrationTests;

/// <summary>
/// KTL-17: the server-side candidate import through HTTP, the durable operation handlers and a
/// real PostgreSQL.
/// </summary>
[Collection(WebHostCollection.Name)]
public sealed class ImportApiTests(PostgreSqlFixture database) : IClassFixture<PostgreSqlFixture>, IDisposable
{
    private const string Issuer = "https://kepler-talento.test/issuer";
    private const string Audience = "kepler-talento-test-api";
    private const string SigningKey = "ktl-17-integration-test-signing-key-00000000000";

    private readonly string _storageRoot = Path.Combine(Path.GetTempPath(), $"ktl-17-import-{Guid.NewGuid():N}");

    public static TheoryData<string, string> ImportEndpoints => new()
    {
        { "POST", "/api/import/batches" },
        { "GET", "/api/import/batches" },
        { "GET", $"/api/import/batches/{Guid.Empty}" },
        { "POST", $"/api/import/batches/{Guid.Empty}/validation" },
        { "POST", $"/api/import/batches/{Guid.Empty}/commit" },
        { "GET", $"/api/import/batches/{Guid.Empty}/rows" },
    };

    public void Dispose()
    {
        // Process-wide, so they would otherwise follow the next test class in the collection.
        Environment.SetEnvironmentVariable("DocumentStorage__Root", null);
        Environment.SetEnvironmentVariable("Import__MaximumRows", null);
        if (Directory.Exists(_storageRoot))
        {
            Directory.Delete(_storageRoot, recursive: true);
        }
    }

    // ---------------------------------------------------------------- authorization (6.3)

    [Theory]
    [MemberData(nameof(ImportEndpoints))]
    public async Task Every_import_endpoint_refuses_an_unauthenticated_caller_before_validation(string method, string path)
    {
        await ResetAsync();
        await using var factory = CreateTokenFactory();
        using var client = factory.CreateClient();

        using var response = await SendMalformedAsync(client, method, path, token: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        await AssertNothingStoredAsync();
    }

    [Theory]
    [MemberData(nameof(ImportEndpoints))]
    public async Task Every_import_endpoint_refuses_a_caller_without_the_permission_before_validation(string method, string path)
    {
        await ResetAsync();
        await using var factory = CreateTokenFactory();
        using var client = factory.CreateClient();
        var token = await MintTokenAsync(client, "readonly-subject", "readonly@example.test");

        using var response = await SendMalformedAsync(client, method, path, token);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.DoesNotContain("errors", await response.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
        await AssertNothingStoredAsync();
    }

    [Theory]
    [MemberData(nameof(ImportEndpoints))]
    public async Task Holding_candidates_create_is_not_an_import_permission(string method, string path)
    {
        await ResetAsync();
        await AddUserAsync("creator-subject", "creator@example.test", "rrhh_user");
        await using var factory = CreateTokenFactory();
        using var client = factory.CreateClient();
        var token = await MintTokenAsync(client, "creator-subject", "creator@example.test");

        using var response = await SendMalformedAsync(client, method, path, token);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task A_forbidden_caller_cannot_tell_an_existing_batch_from_a_missing_one()
    {
        await ResetAsync();
        await AddUserAsync("admin-subject", "admin@example.test", "rrhh_admin");
        await using var factory = CreateTokenFactory();
        using var client = factory.CreateClient();
        var adminToken = await MintTokenAsync(client, "admin-subject", "admin@example.test");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var existing = await UploadBatchAsync(client, Csv("Ana,Ruiz,ana@example.test,,,,"));
        client.DefaultRequestHeaders.Authorization = null;
        var readerToken = await MintTokenAsync(client, "readonly-subject", "readonly@example.test");

        using var forExisting = await SendMalformedAsync(client, "GET", $"/api/import/batches/{existing.Id}/rows", readerToken);
        using var forMissing = await SendMalformedAsync(client, "GET", $"/api/import/batches/{Guid.CreateVersion7()}/rows", readerToken);

        Assert.Equal(HttpStatusCode.Forbidden, forExisting.StatusCode);
        Assert.Equal(forExisting.StatusCode, forMissing.StatusCode);
        Assert.Equal(await forExisting.Content.ReadAsStringAsync(), await forMissing.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task An_administrator_holding_the_permission_reaches_the_import_surface_with_a_real_token()
    {
        await ResetAsync();
        await AddUserAsync("admin-subject", "admin@example.test", "rrhh_admin");
        await using var factory = CreateTokenFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            await MintTokenAsync(client, "admin-subject", "admin@example.test"));

        var batch = await UploadBatchAsync(client, Csv("Ana,Ruiz,ana@example.test,,,,"), "ana-ruiz.csv");
        var page = await client.GetFromJsonAsync<ImportBatchPageResponse>("/api/import/batches");

        Assert.Equal("ana-ruiz.csv", batch.OriginalFileName);
        Assert.True(batch.UploadedByCaller);
        Assert.Equal(batch.Id, Assert.Single(page!.Items).Id);
    }

    // ------------------------------------------------------- upload, scan, quarantine (6.4)

    [Fact]
    public async Task An_upload_is_quarantined_queued_for_scanning_and_reveals_no_storage_location()
    {
        await ResetAsync();
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        using var response = await UploadAsync(client, "candidatos.csv", Encoding.UTF8.GetBytes(Csv("Ana,Ruiz,ana@example.test,,,,")));
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.DoesNotContain("imports/", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("storageKey", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(_storageRoot, body.Replace("\\\\", "\\"), StringComparison.OrdinalIgnoreCase);
        var batch = (await response.Content.ReadFromJsonAsync<ImportBatchResponse>())!;
        Assert.Equal(ImportBatchStates.Uploaded, batch.State);
        await using var db = NewDbContext();
        var stored = await db.ImportBatches.SingleAsync();
        var storage = factory.Services.GetRequiredService<IDocumentStorage>();
        Assert.False(await storage.AvailableExistsAsync(stored.StorageKey, CancellationToken.None));
        Assert.Equal(ImportOperations.Scan, (await db.Operations.SingleAsync()).Type);
    }

    [Fact]
    public async Task A_file_that_is_not_a_csv_by_name_is_refused_and_stores_nothing()
    {
        await ResetAsync();
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        using var response = await UploadAsync(client, "candidatos.xlsx", Encoding.UTF8.GetBytes("first_name"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(ImportErrors.FileTypeNotAllowed, await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        await AssertNothingStoredAsync();
    }

    [Fact]
    public async Task A_file_is_never_read_before_the_scanner_reports_it_clean()
    {
        await ResetAsync();
        var reader = new CountingImportRowReader(new CsvImportRowReader());
        var inspector = new CountingImportFileInspector(new ImportFileInspector());
        await using var factory = CreateFactory(reader: reader, inspector: inspector);
        using var client = factory.CreateClient();
        var uploaded = await UploadBatchAsync(client, Csv("Ana,Ruiz,ana@example.test,,,,"));

        using var early = await ValidateAsync(client, uploaded);

        Assert.Equal(HttpStatusCode.Conflict, early.StatusCode);
        Assert.Contains(ImportErrors.BatchNotReady, await early.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        Assert.Equal(0, reader.Reads);
        Assert.Equal(0, inspector.Inspections);
        await using var db = NewDbContext();
        Assert.False(await db.ImportRowOutcomes.AnyAsync());
    }

    [Fact]
    public async Task An_infected_file_is_refused_terminally_and_no_retry_admits_it()
    {
        await ResetAsync();
        var reader = new CountingImportRowReader(new CsvImportRowReader());
        await using var factory = CreateFactory(scanner: new MarkerMalwareScanner(), reader: reader);
        using var client = factory.CreateClient();
        var uploaded = await UploadBatchAsync(client, Csv("Ana,Ruiz,ana@example.test,SYNTHETIC-MALWARE-MARKER,,,"));

        await RunOperationsAsync(factory.Services);
        var infected = await GetBatchAsync(client, uploaded.Id);
        Assert.Equal(ImportBatchStates.Infected, infected.State);
        Assert.Equal(ImportReasonCodes.ScanInfected, infected.FailureCode);

        using (var validate = await ValidateAsync(client, infected))
        {
            Assert.Equal(HttpStatusCode.Conflict, validate.StatusCode);
            Assert.Contains(ImportErrors.BatchRefused, await validate.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        }
        using (var commit = await CommitAsync(client, infected))
        {
            Assert.Equal(HttpStatusCode.Conflict, commit.StatusCode);
        }

        // A retried scan operation, and recovery, both leave the refusal where it is.
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var operation = await db.Operations.AsNoTracking().SingleAsync();
            await scope.ServiceProvider.GetRequiredService<ImportScanHandler>().HandleAsync(operation, CancellationToken.None);
            await ImportMaintenanceService.RecoverAsync(scope.ServiceProvider, CancellationToken.None);
        }
        await RunOperationsAsync(factory.Services);

        Assert.Equal(ImportBatchStates.Infected, (await GetBatchAsync(client, uploaded.Id)).State);
        Assert.Equal(0, reader.Reads);
        var storage = factory.Services.GetRequiredService<IDocumentStorage>();
        await using var check = NewDbContext();
        Assert.False(await storage.AvailableExistsAsync((await check.ImportBatches.SingleAsync()).StorageKey, CancellationToken.None));
    }

    [Fact]
    public async Task A_file_the_scanner_cannot_judge_is_refused_rather_than_admitted()
    {
        await ResetAsync();
        var reader = new CountingImportRowReader(new CsvImportRowReader());
        await using var factory = CreateFactory(
            scanner: new FakeMalwareScanner(new ScanResult(ScanVerdict.Error, "scanner.unavailable")),
            reader: reader);
        using var client = factory.CreateClient();
        var uploaded = await UploadBatchAsync(client, Csv("Ana,Ruiz,ana@example.test,,,,"));

        await RunOperationsAsync(factory.Services);

        var batch = await GetBatchAsync(client, uploaded.Id);
        Assert.Equal(ImportBatchStates.Unscannable, batch.State);
        using var validate = await ValidateAsync(client, batch);
        Assert.Equal(HttpStatusCode.Conflict, validate.StatusCode);
        Assert.Equal(0, reader.Reads);
    }

    [Fact]
    public async Task A_csv_whose_content_is_a_spreadsheet_container_is_refused_and_not_parsed()
    {
        await ResetAsync();
        var reader = new CountingImportRowReader(new CsvImportRowReader());
        await using var factory = CreateFactory(reader: reader);
        using var client = factory.CreateClient();
        byte[] zip = [0x50, 0x4b, 0x03, 0x04, 0x14, 0x00, 0x00, 0x00, 0x08, 0x00];
        using var upload = await UploadAsync(client, "candidatos.csv", zip);
        var uploaded = (await upload.Content.ReadFromJsonAsync<ImportBatchResponse>())!;
        await RunOperationsAsync(factory.Services);

        using (await ValidateAsync(client, await GetBatchAsync(client, uploaded.Id)))
        {
        }
        await RunOperationsAsync(factory.Services);

        var batch = await GetBatchAsync(client, uploaded.Id);
        Assert.Equal(ImportBatchStates.Failed, batch.State);
        Assert.Equal(ImportReasonCodes.FileContentMismatch, batch.FailureCode);
        Assert.Equal(0, reader.Reads);
    }

    // ------------------------------------------------------------ file contract (structural)

    [Fact]
    public async Task A_missing_required_column_is_a_structural_problem_naming_the_column_and_no_row_is_evaluated()
    {
        await ResetAsync();
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var batch = await UploadAndValidateAsync(client, factory.Services, "first_name,last_name\nZoraida,Villalobos\n");

        Assert.Equal(ImportBatchStates.Failed, batch.State);
        Assert.Equal(ImportReasonCodes.ColumnMissing, batch.FailureCode);
        Assert.Equal("email", batch.FailureDetail);
        await using var db = NewDbContext();
        Assert.False(await db.ImportRowOutcomes.AnyAsync());
    }

    [Fact]
    public async Task A_file_over_the_row_limit_is_refused_naming_the_limit_before_any_row_is_evaluated()
    {
        await ResetAsync();
        await using var factory = CreateFactory(maximumRows: 3);
        using var client = factory.CreateClient();

        var batch = await UploadAndValidateAsync(client, factory.Services, Csv(
            "A,A,a@example.test,,,,",
            "B,B,b@example.test,,,,",
            "C,C,c@example.test,,,,",
            "D,D,d@example.test,,,,"));

        Assert.Equal(ImportBatchStates.Failed, batch.State);
        Assert.Equal(ImportReasonCodes.RowLimitExceeded, batch.FailureCode);
        Assert.Equal("3", batch.FailureDetail);
        await using var db = NewDbContext();
        Assert.False(await db.ImportRowOutcomes.AnyAsync());
    }

    [Fact]
    public async Task A_header_only_file_validates_to_zero_rows_and_its_commit_creates_nothing()
    {
        await ResetAsync();
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var validated = await UploadAndValidateAsync(client, factory.Services, Header + "\n");
        Assert.Equal(ImportBatchStates.Validated, validated.State);
        Assert.Equal(0, validated.RowCount);

        using (var commit = await CommitAsync(client, validated))
        {
            Assert.Equal(HttpStatusCode.Accepted, commit.StatusCode);
        }
        await RunOperationsAsync(factory.Services);

        Assert.Equal(ImportBatchStates.Committed, (await GetBatchAsync(client, validated.Id)).State);
        await using var db = NewDbContext();
        Assert.False(await db.Candidates.AnyAsync());
    }

    // ------------------------------------------------------ dry run, outcomes and report

    [Fact]
    public async Task Validation_is_a_dry_run_whose_outcomes_account_for_every_row_and_carry_no_value()
    {
        await ResetAsync();
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var batch = await UploadAndValidateAsync(client, factory.Services, Csv(
            "Zoraida,Villalobos,zoraida@sentinel.test,600111222,available,2026-03-18,Inglés:B2",
            ",Etxeberria,sin-nombre@sentinel.test,,,,",
            "Marcelino,Arrieta,no-es-un-correo,,,,",
            "Zoraida,Villalobos,ZORAIDA@sentinel.test,,,,",
            "Hermenegilda,Olabarrieta,hermenegilda@sentinel.test,,archivada,,"));

        Assert.Equal(ImportBatchStates.Validated, batch.State);
        Assert.Equal(5, batch.RowCount);
        Assert.Equal(1, batch.LoadedRows);
        Assert.Equal(3, batch.RejectedRows);
        Assert.Equal(1, batch.SkippedRows);
        Assert.Equal(batch.RowCount, batch.LoadedRows + batch.RejectedRows + batch.SkippedRows);

        await using (var db = NewDbContext())
        {
            Assert.False(await db.Candidates.AnyAsync());
            Assert.Equal(5, await db.ImportRowOutcomes.CountAsync());
        }

        using var report = await client.GetAsync($"/api/import/batches/{batch.Id}/rows");
        var text = await report.Content.ReadAsStringAsync();
        foreach (var value in new[] { "Zoraida", "Villalobos", "sentinel", "Etxeberria", "Marcelino", "no-es-un-correo", "archivada", "600111222" })
        {
            Assert.DoesNotContain(value, text, StringComparison.OrdinalIgnoreCase);
        }
        var rows = (await report.Content.ReadFromJsonAsync<ImportRowReportResponse>())!;
        Assert.Equal(ImportPhases.Validation, rows.Phase);
        Assert.Equal([1, 2, 3, 4, 5], rows.Items.Select(row => row.RowNumber));
        Assert.Equal(("first_name", ImportReasonCodes.FieldRequired), (rows.Items[1].Field, rows.Items[1].ReasonCode));
        Assert.Equal(("email", ImportReasonCodes.EmailInvalid), (rows.Items[2].Field, rows.Items[2].ReasonCode));
        Assert.Equal((ImportRowOutcomes.Skipped, ImportReasonCodes.CandidateDuplicate), (rows.Items[3].Outcome, rows.Items[3].ReasonCode));
        Assert.Equal(("status", ImportReasonCodes.StatusUnknown), (rows.Items[4].Field, rows.Items[4].ReasonCode));
    }

    [Fact]
    public async Task Commit_before_validation_is_refused_and_creates_nothing()
    {
        await ResetAsync();
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var uploaded = await UploadBatchAsync(client, Csv("Ana,Ruiz,ana@example.test,,,,"));
        await RunOperationsAsync(factory.Services);

        using var commit = await CommitAsync(client, await GetBatchAsync(client, uploaded.Id));

        Assert.Equal(HttpStatusCode.Conflict, commit.StatusCode);
        Assert.Contains(ImportErrors.BatchNotValidated, await commit.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        await using var db = NewDbContext();
        Assert.False(await db.Candidates.AnyAsync());
    }

    [Fact]
    public async Task Commit_of_a_batch_with_rejected_rows_is_refused()
    {
        await ResetAsync();
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var batch = await UploadAndValidateAsync(client, factory.Services, Csv("Ana,Ruiz,ana@example.test,,,,", ",Gil,luis@example.test,,,,"));

        using var commit = await CommitAsync(client, batch);

        Assert.Equal(HttpStatusCode.Conflict, commit.StatusCode);
        Assert.Contains(ImportErrors.BatchHasRejectedRows, await commit.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    // ------------------------------------------------------ commit, ordinary candidates, idempotency

    [Fact]
    public async Task A_commit_creates_ordinary_audited_candidates_that_read_back_through_the_candidate_endpoint()
    {
        await ResetAsync();
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var batch = await UploadAndValidateAsync(client, factory.Services, Csv(
            "Ana,Ruiz,ana@example.test,600000001,available,2026-03-18,Inglés:B2;Francés:A1",
            "Luis,Gil,luis@example.test,,,,"));

        using (var commit = await CommitAsync(client, batch))
        {
            Assert.Equal(HttpStatusCode.Accepted, commit.StatusCode);
            Assert.Equal(ImportBatchStates.Committing, (await commit.Content.ReadFromJsonAsync<ImportBatchResponse>())!.State);
        }
        await RunOperationsAsync(factory.Services);

        var committed = await GetBatchAsync(client, batch.Id);
        Assert.Equal(ImportBatchStates.Committed, committed.State);
        Assert.Equal(2, committed.LoadedRows);

        await using var db = NewDbContext();
        var candidates = await db.Candidates.AsNoTracking().OrderBy(candidate => candidate.Email).ToListAsync();
        Assert.Equal(["ana@example.test", "luis@example.test"], candidates.Select(candidate => candidate.Email));
        var ana = candidates[0];
        Assert.Null(ana.SourceKey);
        Assert.Equal(CandidateStatuses.Available, ana.Status);
        Assert.Equal(new DateOnly(2026, 3, 18), ana.ConsentAt);
        Assert.Null(candidates[1].ConsentAt);

        var created = await db.AuditEvents.AsNoTracking().Where(audit => audit.EventType == CandidateAuditEvents.Created).ToListAsync();
        Assert.Equal(candidates.Select(candidate => candidate.Id.ToString("N")).Order(), created.Select(audit => audit.SubjectId).Order());

        var readBack = await client.GetFromJsonAsync<CandidateResponse>($"/api/candidates/{ana.Id}");
        Assert.Equal("Ruiz", readBack!.LastName);
        Assert.Equal(["Francés", "Inglés"], readBack.Languages.Select(language => language.Language).Order());
    }

    [Fact]
    public async Task Committing_a_batch_twice_creates_each_candidate_once()
    {
        await ResetAsync();
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var rows = Enumerable.Range(1, 25).Select(index => $"Nombre{index},Apellido{index},persona{index}@example.test,,,,").ToArray();
        var batch = await UploadAndValidateAsync(client, factory.Services, Csv(rows));

        using (await CommitAsync(client, batch))
        {
        }
        await RunOperationsAsync(factory.Services);
        var first = await GetBatchAsync(client, batch.Id);

        using (var again = await CommitAsync(client, first))
        {
            Assert.Equal(HttpStatusCode.Accepted, again.StatusCode);
            Assert.Equal(ImportBatchStates.Committed, (await again.Content.ReadFromJsonAsync<ImportBatchResponse>())!.State);
        }
        using (var stale = await CommitAsync(client, batch))
        {
            Assert.Equal(ImportBatchStates.Committed, (await stale.Content.ReadFromJsonAsync<ImportBatchResponse>())!.State);
        }
        // The same durable operation executed a second time, as a retry after a lost lease would.
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var operation = await db.Operations.AsNoTracking().SingleAsync(value => value.Type == ImportOperations.Commit);
            await scope.ServiceProvider.GetRequiredService<ImportCommitHandler>().HandleAsync(operation, CancellationToken.None);
        }
        Assert.Equal(0, await RunOperationsAsync(factory.Services));

        await using var check = NewDbContext();
        Assert.Equal(25, await check.Candidates.CountAsync());
        Assert.Equal(25, await check.Candidates.Select(candidate => candidate.Email).Distinct().CountAsync());
        Assert.Equal(25, await check.ImportRowOutcomes.CountAsync(outcome => outcome.Phase == ImportPhases.Commit));
        Assert.Equal(25, await check.AuditEvents.CountAsync(audit => audit.EventType == CandidateAuditEvents.Created));
    }

    [Fact]
    public async Task A_commit_resumed_after_partial_progress_writes_only_the_remaining_rows()
    {
        await ResetAsync();
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var rows = Enumerable.Range(1, 12).Select(index => $"Nombre{index},Apellido{index},persona{index}@example.test,,,,").ToArray();
        var batch = await UploadAndValidateAsync(client, factory.Services, Csv(rows));
        using (await CommitAsync(client, batch))
        {
        }

        // Simulate what an interrupted run leaves: the first five rows fully written, each with
        // its outcome, and nothing else. (The real kill-the-process proof is ImportInterruptedCommitTests.)
        await using (var db = NewDbContext())
        {
            for (var row = 1; row <= 5; row++)
            {
                var candidate = CandidateFactory.Create(
                    new CreateCandidateCommand($"Nombre{row}", $"Apellido{row}", "", $"persona{row}@example.test", "", "", "", "", "new", "", "", null, null, null),
                    DateTimeOffset.UtcNow);
                db.Candidates.Add(candidate);
                db.ImportRowOutcomes.Add(new ImportRowOutcome(Guid.CreateVersion7(), batch.Id, ImportPhases.Commit, row, ImportRowOutcomes.Loaded, null, null, candidate.Id, DateTimeOffset.UtcNow));
            }
            await db.SaveChangesAsync();
        }
        await RunOperationsAsync(factory.Services);

        var committed = await GetBatchAsync(client, batch.Id);
        Assert.Equal(ImportBatchStates.Committed, committed.State);
        Assert.Equal(12, committed.LoadedRows);
        await using var check = NewDbContext();
        Assert.Equal(12, await check.Candidates.CountAsync());
        Assert.Equal(12, await check.Candidates.Select(candidate => candidate.Email).Distinct().CountAsync());
    }

    [Fact]
    public async Task The_same_file_uploaded_again_after_a_commit_skips_every_row_and_says_it_was_imported()
    {
        await ResetAsync();
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var csv = Csv("Ana,Ruiz,ana@example.test,,,,", "Luis,Gil,luis@example.test,,,,");
        var first = await UploadAndValidateAsync(client, factory.Services, csv);
        using (await CommitAsync(client, first))
        {
        }
        await RunOperationsAsync(factory.Services);

        var second = await UploadAndValidateAsync(client, factory.Services, csv);

        Assert.Equal(ImportBatchStates.Validated, second.State);
        Assert.Equal(2, second.SkippedRows);
        Assert.Equal(0, second.RejectedRows);
        Assert.NotNull(second.SameFileCommittedAt);
        using (await CommitAsync(client, second))
        {
        }
        await RunOperationsAsync(factory.Services);
        Assert.Equal(2, (await GetBatchAsync(client, second.Id)).SkippedRows);
        await using var db = NewDbContext();
        Assert.Equal(2, await db.Candidates.CountAsync());
    }

    // ------------------------------------------------------------- catalog references (6.7, 6.8)

    [Fact]
    public async Task No_import_run_creates_renames_or_reactivates_a_catalog_entry()
    {
        await ResetAsync();
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await using (var db = NewDbContext())
        {
            var retired = await db.CatalogItems.FirstAsync(item => item.Family == "language" && item.NameEs == "Italiano");
            retired.SetActive(false, DateTimeOffset.UtcNow);
            await db.SaveChangesAsync();
        }
        var before = await CatalogSnapshotAsync();

        var batch = await UploadAndValidateAsync(client, factory.Services, Csv(
            "Ana,Ruiz,ana@example.test,,,,Klingon:B2",
            "Luis,Gil,luis@example.test,,,,Inglés:Nativo",
            "Eva,Sanz,eva@example.test,,,,Italiano:B1"));
        var clean = await UploadAndValidateAsync(client, factory.Services, Csv("Eva,Sanz,eva@example.test,,,,Italiano:B1"));
        using (await CommitAsync(client, clean))
        {
        }
        await RunOperationsAsync(factory.Services);

        Assert.Equal(before, await CatalogSnapshotAsync());
        Assert.Equal(2, batch.RejectedRows);
        Assert.Contains(batch.UnresolvedValues, value => value.Family == "language" && value.Value == "Klingon");
        Assert.Contains(batch.UnresolvedValues, value => value.Family == "language_level" && value.Value == "Nativo");
    }

    [Fact]
    public async Task A_row_whose_reference_resolves_to_nothing_is_rejected_and_never_loads_without_it()
    {
        await ResetAsync();
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var batch = await UploadAndValidateAsync(client, factory.Services, Csv("Ana,Ruiz,ana@example.test,,,,Klingon:B2"));

        var report = await client.GetFromJsonAsync<ImportRowReportResponse>($"/api/import/batches/{batch.Id}/rows");
        var row = Assert.Single(report!.Items);
        Assert.Equal((ImportRowOutcomes.Rejected, "languages", ImportReasonCodes.ReferenceUnresolved), (row.Outcome, row.Field, row.ReasonCode));
        using var commit = await CommitAsync(client, batch);
        Assert.Equal(HttpStatusCode.Conflict, commit.StatusCode);
        await using var db = NewDbContext();
        Assert.False(await db.Candidates.AnyAsync());
    }

    // ---------------------------------------------------------------------- grants (6.9)

    [Theory]
    [InlineData("ADM_ImportRowOutcomes", "UPDATE", false)]
    [InlineData("ADM_ImportRowOutcomes", "DELETE", false)]
    [InlineData("ADM_ImportRowOutcomes", "TRUNCATE", false)]
    [InlineData("ADM_ImportRowOutcomes", "SELECT", true)]
    [InlineData("ADM_ImportRowOutcomes", "INSERT", true)]
    [InlineData("ADM_ImportBatches", "DELETE", false)]
    [InlineData("ADM_ImportBatches", "TRUNCATE", false)]
    [InlineData("ADM_ImportBatches", "SELECT", true)]
    [InlineData("ADM_ImportBatches", "INSERT", true)]
    [InlineData("ADM_ImportBatches", "UPDATE", true)]
    public async Task The_runtime_role_holds_exactly_the_approved_privileges_on_the_import_tables(string table, string privilege, bool expected)
    {
        await ResetAsync();
        await using var connection = new NpgsqlConnection(database.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("SELECT has_table_privilege('ktl_runtime', @table, @privilege)", connection);
        command.Parameters.AddWithValue("table", $"public.\"{table}\"");
        command.Parameters.AddWithValue("privilege", privilege);

        Assert.Equal(expected, (bool)(await command.ExecuteScalarAsync())!);
    }

    // ---------------------------------------------------------------------- purge (6.11)

    [Fact]
    public async Task A_purge_removes_a_closed_batch_file_past_its_window_keeps_its_report_and_leaves_no_orphan()
    {
        await ResetAsync();
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var batch = await UploadAndValidateAsync(client, factory.Services, Csv("Ana,Ruiz,ana@example.test,,,,"));
        using (await CommitAsync(client, batch))
        {
        }
        await RunOperationsAsync(factory.Services);
        var recent = await UploadAndValidateAsync(client, factory.Services, Csv("Luis,Gil,luis@example.test,,,,"));

        string oldKey;
        await using (var db = NewDbContext())
        {
            oldKey = (await db.ImportBatches.SingleAsync(value => value.Id == batch.Id)).StorageKey;
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE \"ADM_ImportBatches\" SET \"ClosedAtUtc\" = now() - interval '31 days' WHERE \"Id\" = {batch.Id}");
        }
        var storage = factory.Services.GetRequiredService<IDocumentStorage>();
        var orphanKey = $"imports/{Guid.NewGuid():N}/content";
        await storage.WriteQuarantineAsync(orphanKey, new MemoryStream("first_name"u8.ToArray()), 1024, CancellationToken.None);
        File.SetLastWriteTimeUtc(Path.Combine(_storageRoot, "quarantine", "imports", orphanKey.Split('/')[1], "content"), DateTime.UtcNow.AddDays(-3));

        ImportPurgeReport report;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            report = await scope.ServiceProvider.GetRequiredService<ImportPurgeHandler>().PurgeAsync(DateTimeOffset.UtcNow, CancellationToken.None);
        }

        Assert.Equal(1, report.FilesPurged);
        Assert.Equal(1, report.OrphansRemoved);
        Assert.False(await storage.AvailableExistsAsync(oldKey, CancellationToken.None));
        var purged = await GetBatchAsync(client, batch.Id);
        Assert.Equal(ImportBatchStates.Expired, purged.State);
        Assert.False(purged.FileRetained);
        Assert.Equal(1, purged.LoadedRows);
        var rows = await client.GetFromJsonAsync<ImportRowReportResponse>($"/api/import/batches/{batch.Id}/rows");
        Assert.Equal(ImportPhases.Commit, rows!.Phase);
        Assert.Single(rows.Items);
        Assert.True((await GetBatchAsync(client, recent.Id)).FileRetained);

        // No batch points at a file that is gone without saying so.
        await using (var db = NewDbContext())
        {
            foreach (var live in await db.ImportBatches.Where(value => value.FilePurgedAtUtc == null).ToListAsync())
            {
                Assert.True(await storage.AvailableExistsAsync(live.StorageKey, CancellationToken.None));
            }
        }
        await using var quarantine = NewDbContext();
        Assert.False(File.Exists(Path.Combine(_storageRoot, "quarantine", "imports", orphanKey.Split('/')[1], "content")));
    }

    [Fact]
    public async Task A_closed_batch_whose_file_disappeared_reports_it_rather_than_failing_obscurely()
    {
        await ResetAsync();
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var batch = await UploadAndValidateAsync(client, factory.Services, Csv("Ana,Ruiz,ana@example.test,,,,"));
        var storage = factory.Services.GetRequiredService<IDocumentStorage>();
        await using (var db = NewDbContext())
        {
            await storage.DeleteAvailableIfExistsAsync((await db.ImportBatches.SingleAsync()).StorageKey, CancellationToken.None);
        }

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<ImportPurgeHandler>().PurgeAsync(DateTimeOffset.UtcNow, CancellationToken.None);
        }

        var reported = await GetBatchAsync(client, batch.Id);
        Assert.Equal(ImportBatchStates.Expired, reported.State);
        Assert.False(reported.FileRetained);
        using var commit = await CommitAsync(client, reported);
        Assert.Equal(HttpStatusCode.Conflict, commit.StatusCode);
        Assert.Contains(ImportErrors.BatchExpired, await commit.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------------- logs (6.10)

    [Fact]
    public async Task No_imported_value_file_name_or_content_reaches_the_logs_on_success_or_failure()
    {
        await ResetAsync();
        var captured = new CapturingLoggerProvider();
        await using var factory = CreateFactory(loggerProvider: captured);
        using var client = factory.CreateClient();
        const string fileName = "candidatos-zoraida-villalobos.csv";

        // Success path: validate and commit.
        var good = await UploadAndValidateAsync(client, factory.Services, Csv("Zoraida,Villalobos,zoraida@sentinel.test,600111222,,2026-03-18,Inglés:B2"), fileName);
        using (await CommitAsync(client, good))
        {
        }
        await RunOperationsAsync(factory.Services);
        // Row failures, an unresolved reference and a structural refusal.
        await UploadAndValidateAsync(client, factory.Services, Csv("Marcelino,Arrieta,marcelino@@sentinel.test,,,,Klingon-Centinela:B2", ",Etxeberria,etx@sentinel.test,,,,"), fileName);
        await UploadAndValidateAsync(client, factory.Services, "first_name,apellido_centinela\nHermenegilda,Olabarrieta\n", fileName);

        var logs = captured.Rendered();
        Assert.NotEmpty(logs);
        foreach (var value in new[] { "Zoraida", "Villalobos", "sentinel", "600111222", "Marcelino", "Arrieta", "Etxeberria", "Hermenegilda", "Olabarrieta", "zoraida-villalobos" })
        {
            Assert.DoesNotContain(value, logs, StringComparison.OrdinalIgnoreCase);
        }
        Assert.Contains(good.Id.ToString(), logs, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(ImportReasonCodes.EmailInvalid, logs, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------------- helpers

    private async Task AssertNothingStoredAsync()
    {
        await using var db = NewDbContext();
        Assert.False(await db.ImportBatches.AnyAsync());
        Assert.False(await db.ImportRowOutcomes.AnyAsync());
        Assert.False(await db.Operations.AnyAsync());
        Assert.False(Directory.Exists(Path.Combine(_storageRoot, "quarantine", "imports"))
            && Directory.EnumerateFiles(Path.Combine(_storageRoot, "quarantine", "imports"), "*", SearchOption.AllDirectories).Any());
    }

    private async Task<string> CatalogSnapshotAsync()
    {
        await using var db = NewDbContext();
        var items = await db.CatalogItems.AsNoTracking()
            .OrderBy(item => item.Id)
            .Select(item => new { item.Id, item.Family, item.Code, item.NameEs, item.IsActive, item.UpdatedAtUtc })
            .ToListAsync();
        return string.Join('\n', items.Select(item => $"{item.Id}|{item.Family}|{item.Code}|{item.NameEs}|{item.IsActive}|{item.UpdatedAtUtc:O}"));
    }

    private static async Task<HttpResponseMessage> SendMalformedAsync(HttpClient client, string method, string path, string? token)
    {
        using var request = new HttpRequestMessage(new HttpMethod(method), path);
        if (method == "POST")
        {
            // Deliberately malformed: not multipart for the upload, not JSON for the transitions.
            request.Content = new StringContent("{ not json", Encoding.UTF8, "application/json");
        }
        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
        return await client.SendAsync(request);
    }

    private static async Task<string> MintTokenAsync(HttpClient client, string subject, string email)
    {
        using var response = await client.PostAsJsonAsync("/api/dev/token", new { subject, displayName = "Integration Caller", email });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<DevelopmentTokenResponse>())!.AccessToken;
    }

    private async Task ResetAsync()
    {
        await using var dbContext = NewDbContext();
        await dbContext.Database.EnsureDeletedAsync();
        await DatabaseInitializer.MigrateAsync(dbContext, CancellationToken.None);
        await DatabaseInitializer.SeedCatalogsAsync(dbContext, CancellationToken.None);
        await ImportTestActor.SeedUserAsync(dbContext);
        if (Directory.Exists(_storageRoot))
        {
            Directory.Delete(_storageRoot, recursive: true);
        }
    }

    private async Task AddUserAsync(string subject, string email, string roleName)
    {
        await using var dbContext = NewDbContext();
        dbContext.Users.Add(new User(Guid.CreateVersion7(), subject, "Integration User", email, roleName, DateTimeOffset.UtcNow));
        await dbContext.SaveChangesAsync();
    }

    private ApplicationDbContext NewDbContext() => new(
        new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(database.ConnectionString).Options);

    private void SetSharedEnvironment()
    {
        // Infrastructure reads these while registering services, before the factory's in-memory
        // configuration applies — see WebHostCollection.
        Environment.SetEnvironmentVariable("ConnectionStrings__ApplicationDatabase", database.ConnectionString);
        Environment.SetEnvironmentVariable("DocumentStorage__Root", _storageRoot);
        Environment.SetEnvironmentVariable("OperationWorker__Enabled", "false");
    }

    private WebApplicationFactory<Program> CreateFactory(
        IMalwareScanner? scanner = null,
        IImportRowReader? reader = null,
        IImportFileInspector? inspector = null,
        int? maximumRows = null,
        ILoggerProvider? loggerProvider = null)
    {
        SetSharedEnvironment();
        Environment.SetEnvironmentVariable("DevelopmentActor__Enabled", "true");
        Environment.SetEnvironmentVariable("Import__MaximumRows", maximumRows?.ToString(System.Globalization.CultureInfo.InvariantCulture));
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
                services.AddSingleton(scanner ?? new FakeMalwareScanner());
                if (reader is not null)
                {
                    services.RemoveAll<IImportRowReader>();
                    services.AddSingleton(reader);
                }
                if (inspector is not null)
                {
                    services.RemoveAll<IImportFileInspector>();
                    services.AddSingleton(inspector);
                }
                if (loggerProvider is not null)
                {
                    // Serilog owns the logger factory; route every ILogger<T> through the same
                    // redaction enricher production uses and into the capture.
                    services.RemoveAll<ILoggerFactory>();
                    services.AddSingleton<ILoggerFactory>(_ => new Serilog.Extensions.Logging.SerilogLoggerFactory(
                        new Serilog.LoggerConfiguration()
                            .MinimumLevel.Verbose()
                            .Enrich.With<KeplerTalento.Web.Observability.PersonalDataRedactionEnricher>()
                            .WriteTo.Logger(configuration => configuration.WriteTo.Sink(new SerilogCapture((CapturingLoggerProvider)loggerProvider)))
                            .CreateLogger(),
                        dispose: true));
                }
            });
        });
    }

    private WebApplicationFactory<Program> CreateTokenFactory()
    {
        SetSharedEnvironment();
        Environment.SetEnvironmentVariable("DevelopmentActor__Enabled", "false");
        Environment.SetEnvironmentVariable("Import__MaximumRows", null);
        Environment.SetEnvironmentVariable("Authentication__SubjectClaim", "oid");
        Environment.SetEnvironmentVariable("Authentication__DevelopmentIssuer__Enabled", "true");
        Environment.SetEnvironmentVariable("Authentication__DevelopmentIssuer__SigningKey", SigningKey);
        Environment.SetEnvironmentVariable("Authentication__DevelopmentIssuer__Issuer", Issuer);
        Environment.SetEnvironmentVariable("Authentication__DevelopmentIssuer__Audience", Audience);
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ConnectionStrings:ApplicationDatabase"] = database.ConnectionString,
                    ["DevelopmentActor:Enabled"] = "false",
                    ["Authentication:SubjectClaim"] = "oid",
                    ["Authentication:DevelopmentIssuer:Enabled"] = "true",
                    ["Authentication:DevelopmentIssuer:SigningKey"] = SigningKey,
                    ["Authentication:DevelopmentIssuer:Issuer"] = Issuer,
                    ["Authentication:DevelopmentIssuer:Audience"] = Audience,
                    ["ProxyTrust:KnownNetworks:0"] = "127.0.0.0/8",
                }));
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IMalwareScanner>();
                services.AddSingleton<IMalwareScanner>(new FakeMalwareScanner());
            });
        });
    }

    private sealed record DevelopmentTokenResponse(string AccessToken);

    /// <summary>Collects rendered log events, message and properties both.</summary>
    internal sealed class CapturingLoggerProvider : ILoggerProvider
    {
        private readonly System.Collections.Concurrent.ConcurrentQueue<string> _lines = new();

        public void Add(string line) => _lines.Enqueue(line);

        public string Rendered() => string.Join('\n', _lines);

        public ILogger CreateLogger(string categoryName) => Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;

        public void Dispose()
        {
        }
    }

    private sealed class SerilogCapture(CapturingLoggerProvider capture) : Serilog.Core.ILogEventSink
    {
        public void Emit(Serilog.Events.LogEvent logEvent)
        {
            var properties = string.Join(' ', logEvent.Properties.Select(property => $"{property.Key}={property.Value}"));
            capture.Add($"{logEvent.RenderMessage()} {properties} {logEvent.Exception}");
        }
    }
}
