using System.Net;
using System.Net.Http.Json;
using KeplerTalento.Application.Abstractions.Identity;
using KeplerTalento.Application.Features.Candidates;
using KeplerTalento.Domain.Candidates;
using KeplerTalento.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using Xunit;

namespace KeplerTalento.Tests.IntegrationTests;

/// <summary>
/// Exercises the candidate slices through the real HTTP → Application → PostgreSQL path
/// against a disposable PostgreSQL instance.
/// </summary>
[Collection(WebHostCollection.Name)]
public sealed class CandidateApiTests(PostgreSqlFixture database) : IClassFixture<PostgreSqlFixture>
{
    [Fact]
    public async Task Candidate_lifecycle_runs_through_http_application_and_postgresql()
    {
        await ResetAsync();
        await using var factory = CreateFactory(TestActor.Full);
        using var client = factory.CreateClient();

        // create — with consent and retention metadata supplied
        var createResponse = await client.PostAsJsonAsync("/api/candidates", new
        {
            firstName = "Laura",
            lastName = "García",
            phone = "+34 600 100 200",
            email = "laura.garcia@example.invalid",
            location = "Madrid",
            province = "Madrid",
            country = "España",
            availability = "Inmediata",
            status = CandidateStatuses.Available,
            source = "LinkedIn",
            notes = "Perfil administrativo.",
            receivedAt = "2026-05-10",
            consentAt = "2026-05-11",
            reviewDueAt = "2027-05-10",
        });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = (await createResponse.Content.ReadFromJsonAsync<CandidateResponse>())!;
        Assert.True(created.IsActive);
        Assert.Equal("García", created.LastName);

        // read by identifier returns exactly what was submitted
        var read = (await client.GetFromJsonAsync<CandidateResponse>($"/api/candidates/{created.Id}"))!;
        Assert.Equal("Laura", read.FirstName);
        Assert.Equal("2026-05-10", read.ReceivedAt);
        Assert.Equal("2026-05-11", read.ConsentAt);
        Assert.Equal("2027-05-10", read.ReviewDueAt);
        Assert.Empty(read.Languages);

        // update — only the phone changes; metadata is not supplied
        var updateResponse = await client.PutAsJsonAsync($"/api/candidates/{created.Id}", new
        {
            firstName = "Laura",
            lastName = "García",
            phone = "+34 600 999 888",
            email = "laura.garcia@example.invalid",
            location = "Madrid",
            province = "Madrid",
            country = "España",
            availability = "Inmediata",
            status = CandidateStatuses.Available,
            source = "LinkedIn",
            notes = "Perfil administrativo.",
            version = read.Version,
        });
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        var updated = (await updateResponse.Content.ReadFromJsonAsync<CandidateResponse>())!;
        Assert.Equal("+34 600 999 888", updated.Phone);
        // The identifier and creation timestamp are untouched, and so is the metadata.
        // Both compared values come from storage: the create response carries the
        // in-memory instant, which PostgreSQL rounds to microseconds on the way in.
        Assert.Equal(created.Id, updated.Id);
        Assert.Equal(read.CreatedAt, updated.CreatedAt);
        Assert.Equal("2026-05-11", updated.ConsentAt);

        // relation write — resolved from catalog names, not identifiers
        var languagesResponse = await client.PutAsJsonAsync($"/api/candidates/{created.Id}/languages", new
        {
            languages = new[] { new { language = "Inglés", level = "B2", certification = "Cambridge" } },
            version = updated.Version,
        });
        Assert.Equal(HttpStatusCode.OK, languagesResponse.StatusCode);
        var withLanguage = (await languagesResponse.Content.ReadFromJsonAsync<CandidateResponse>())!;
        var language = Assert.Single(withLanguage.Languages);
        Assert.Equal("Inglés", language.Language);
        Assert.Equal("B2", language.Level);
        Assert.Equal("Cambridge", language.Certification);

        // logical delete
        var removeResponse = await client.PutAsJsonAsync(
            $"/api/candidates/{created.Id}/active",
            new { isActive = false, version = withLanguage.Version });
        Assert.Equal(HttpStatusCode.OK, removeResponse.StatusCode);
        var removed = (await removeResponse.Content.ReadFromJsonAsync<CandidateResponse>())!;
        Assert.False(removed.IsActive);

        // default list excludes it; read by identifier still returns it, intact
        var listed = (await client.GetFromJsonAsync<CandidateSummaryResponse[]>("/api/candidates"))!;
        Assert.DoesNotContain(listed, summary => summary.Id == created.Id);
        var withRemoved = (await client.GetFromJsonAsync<CandidateSummaryResponse[]>(
            "/api/candidates?includeInactive=true"))!;
        Assert.Contains(withRemoved, summary => summary.Id == created.Id && !summary.IsActive);
        var afterRemoval = (await client.GetFromJsonAsync<CandidateResponse>($"/api/candidates/{created.Id}"))!;
        Assert.Equal("Laura", afterRemoval.FirstName);
        Assert.Single(afterRemoval.Languages);

        // the row is preserved, with the moment of removal recorded
        var stored = await ReadAsync(created.Id);
        Assert.False(stored.IsActive);
        Assert.NotNull(stored.DeletedAtUtc);

        // restore
        var restoreResponse = await client.PutAsJsonAsync(
            $"/api/candidates/{created.Id}/active",
            new { isActive = true, version = afterRemoval.Version });
        Assert.Equal(HttpStatusCode.OK, restoreResponse.StatusCode);
        var restored = (await restoreResponse.Content.ReadFromJsonAsync<CandidateResponse>())!;
        Assert.True(restored.IsActive);
        Assert.Null((await ReadAsync(created.Id)).DeletedAtUtc);
        var listedAgain = (await client.GetFromJsonAsync<CandidateSummaryResponse[]>("/api/candidates"))!;
        Assert.Contains(listedAgain, summary => summary.Id == created.Id);
    }

    [Fact]
    public async Task A_concurrent_update_is_refused_with_a_stable_conflict_code()
    {
        await ResetAsync();
        await using var factory = CreateFactory(TestActor.Full);
        using var client = factory.CreateClient();
        var created = await CreateAsync(client, "Ana", "Lopez");

        // Two actors read the same version; the first write wins.
        var first = await client.PutAsJsonAsync(
            $"/api/candidates/{created.Id}",
            UpdatePayload("Ana", "Lopez", "+34 600 111 111", created.Version));
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var second = await client.PutAsJsonAsync(
            $"/api/candidates/{created.Id}",
            UpdatePayload("Ana", "Lopez", "+34 600 222 222", created.Version));

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        var body = await second.Content.ReadAsStringAsync();
        Assert.Contains(CandidateErrors.ConcurrencyConflict, body, StringComparison.Ordinal);
        // The first actor's values are what is stored.
        Assert.Equal("+34 600 111 111", (await ReadAsync(created.Id)).Phone);
    }

    [Fact]
    public async Task Removal_advances_the_token_so_a_stale_editor_conflicts_rather_than_resurrecting()
    {
        await ResetAsync();
        await using var factory = CreateFactory(TestActor.Full);
        using var client = factory.CreateClient();
        var created = await CreateAsync(client, "Bea", "Mora");

        await client.PutAsJsonAsync(
            $"/api/candidates/{created.Id}/active",
            new { isActive = false, version = created.Version });

        var stale = await client.PutAsJsonAsync(
            $"/api/candidates/{created.Id}",
            UpdatePayload("Bea", "Mora", "+34 600 333 333", created.Version));

        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.False((await ReadAsync(created.Id)).IsActive);
    }

    [Fact]
    public async Task A_relation_naming_an_unknown_catalog_value_is_refused_and_creates_nothing()
    {
        await ResetAsync();
        await using var factory = CreateFactory(TestActor.Full);
        using var client = factory.CreateClient();
        var created = await CreateAsync(client, "Carla", "Gil");

        var response = await client.PutAsJsonAsync($"/api/candidates/{created.Id}/languages", new
        {
            languages = new[] { new { language = "Klingon", level = "B2" } },
            version = created.Version,
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(
            CandidateErrors.CatalogValueUnknown,
            await response.Content.ReadAsStringAsync(),
            StringComparison.Ordinal);
        await using var dbContext = NewDbContext();
        Assert.False(await dbContext.CatalogItems.AnyAsync(item => item.NameEs == "Klingon"));
    }

    [Fact]
    public async Task Document_metadata_round_trips_without_exposing_a_storage_location()
    {
        await ResetAsync();
        await using var factory = CreateFactory(TestActor.Full);
        using var client = factory.CreateClient();
        var created = await CreateAsync(client, "Noa", "Vidal");

        var response = await client.PutAsJsonAsync($"/api/candidates/{created.Id}/documents", new
        {
            documents = new[]
            {
                new { documentType = "CV", originalFilename = "cv.pdf", mimeType = "application/pdf", sizeBytes = 1024, isPrimary = true },
            },
            version = created.Version,
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        var withDocument = (await client.GetFromJsonAsync<CandidateResponse>($"/api/candidates/{created.Id}"))!;
        var document = Assert.Single(withDocument.Documents);
        Assert.Equal("cv.pdf", document.OriginalFilename);
        Assert.True(document.IsPrimary);
        // The storage key exists in the database but must never reach the caller.
        var stored = await StoredDocumentKeyAsync(created.Id);
        Assert.NotEmpty(stored);
        Assert.DoesNotContain(stored, body, StringComparison.Ordinal);
        Assert.DoesNotContain("storageKey", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("candidates/", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Marking_a_second_document_primary_demotes_the_first()
    {
        await ResetAsync();
        await using var factory = CreateFactory(TestActor.Full);
        using var client = factory.CreateClient();
        var created = await CreateAsync(client, "Eva", "Soler");

        var first = await client.PutAsJsonAsync($"/api/candidates/{created.Id}/documents", new
        {
            documents = new[]
            {
                new { documentType = "CV", originalFilename = "uno.pdf", mimeType = "application/pdf", sizeBytes = 10, isPrimary = true },
                new { documentType = "CV", originalFilename = "dos.pdf", mimeType = "application/pdf", sizeBytes = 20, isPrimary = false },
            },
            version = created.Version,
        });
        var withBoth = (await first.Content.ReadFromJsonAsync<CandidateResponse>())!;
        var one = withBoth.Documents.Single(document => document.OriginalFilename == "uno.pdf");
        var two = withBoth.Documents.Single(document => document.OriginalFilename == "dos.pdf");

        // The primary flag moves from one document to the other in a single write. The
        // index enforcing "at most one primary" is not deferrable, so this is the case
        // that proves the ordered two-phase write.
        var swap = await client.PutAsJsonAsync($"/api/candidates/{created.Id}/documents", new
        {
            documents = new[]
            {
                new { id = one.Id, documentType = "CV", originalFilename = "uno.pdf", mimeType = "application/pdf", sizeBytes = 10, isPrimary = false },
                new { id = two.Id, documentType = "CV", originalFilename = "dos.pdf", mimeType = "application/pdf", sizeBytes = 20, isPrimary = true },
            },
            version = withBoth.Version,
        });

        Assert.Equal(HttpStatusCode.OK, swap.StatusCode);
        var after = (await swap.Content.ReadFromJsonAsync<CandidateResponse>())!;
        Assert.Single(after.Documents, document => document.IsPrimary);
        Assert.True(after.Documents.Single(document => document.Id == two.Id).IsPrimary);
    }

    [Fact]
    public async Task Every_candidate_capability_fails_closed_without_it()
    {
        await ResetAsync();
        await using var seedFactory = CreateFactory(TestActor.Full);
        using var seedClient = seedFactory.CreateClient();
        var created = await CreateAsync(seedClient, "Ana", "Lopez");

        foreach (var actor in new[] { TestActor.Unauthenticated, TestActor.None })
        {
            await using var factory = CreateFactory(actor);
            using var client = factory.CreateClient();

            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/candidates")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/api/candidates/{created.Id}")).StatusCode);
            Assert.Equal(
                HttpStatusCode.Forbidden,
                (await client.PostAsJsonAsync("/api/candidates", CreatePayload("X", "Y"))).StatusCode);
            Assert.Equal(
                HttpStatusCode.Forbidden,
                (await client.PutAsJsonAsync($"/api/candidates/{created.Id}", UpdatePayload("X", "Y", "", created.Version))).StatusCode);
            Assert.Equal(
                HttpStatusCode.Forbidden,
                (await client.PutAsJsonAsync($"/api/candidates/{created.Id}/active", new { isActive = false, version = created.Version })).StatusCode);
        }
    }

    [Fact]
    public async Task An_actor_with_read_but_no_write_capability_cannot_change_anything()
    {
        await ResetAsync();
        await using var seedFactory = CreateFactory(TestActor.Full);
        using var seedClient = seedFactory.CreateClient();
        var created = await CreateAsync(seedClient, "Ana", "Lopez");

        await using var factory = CreateFactory(TestActor.Reader);
        using var client = factory.CreateClient();

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/candidates/{created.Id}")).StatusCode);
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await client.PostAsJsonAsync("/api/candidates", CreatePayload("X", "Y"))).StatusCode);
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await client.PutAsJsonAsync($"/api/candidates/{created.Id}", UpdatePayload("X", "Y", "", created.Version))).StatusCode);
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await client.PutAsJsonAsync($"/api/candidates/{created.Id}/active", new { isActive = false, version = created.Version })).StatusCode);
        Assert.Equal("Ana", (await ReadAsync(created.Id)).FirstName);
    }

    [Fact]
    public async Task An_actor_who_may_update_may_not_remove()
    {
        await ResetAsync();
        await using var seedFactory = CreateFactory(TestActor.Full);
        using var seedClient = seedFactory.CreateClient();
        var created = await CreateAsync(seedClient, "Ana", "Lopez");

        await using var factory = CreateFactory(TestActor.Editor);
        using var client = factory.CreateClient();

        var refusal = await client.PutAsJsonAsync(
            $"/api/candidates/{created.Id}/active",
            new { isActive = false, version = created.Version });

        Assert.Equal(HttpStatusCode.Forbidden, refusal.StatusCode);
        Assert.True((await ReadAsync(created.Id)).IsActive);
    }

    [Fact]
    public async Task A_refusal_does_not_disclose_whether_the_candidate_exists()
    {
        await ResetAsync();
        await using var seedFactory = CreateFactory(TestActor.Full);
        using var seedClient = seedFactory.CreateClient();
        var created = await CreateAsync(seedClient, "Ana", "Lopez");

        await using var factory = CreateFactory(TestActor.None);
        using var client = factory.CreateClient();

        var forExisting = await client.GetAsync($"/api/candidates/{created.Id}");
        var forMissing = await client.GetAsync($"/api/candidates/{Guid.NewGuid()}");

        Assert.Equal(forExisting.StatusCode, forMissing.StatusCode);
        var existingBody = Scrub(await forExisting.Content.ReadAsStringAsync());
        var missingBody = Scrub(await forMissing.Content.ReadAsStringAsync());
        Assert.Equal(existingBody, missingBody);
        Assert.DoesNotContain("Ana", existingBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task There_is_no_delete_verb_anywhere_in_the_candidate_group()
    {
        await ResetAsync();
        await using var factory = CreateFactory(TestActor.Full);
        using var client = factory.CreateClient();
        var created = await CreateAsync(client, "Ana", "Lopez");

        foreach (var route in new[]
        {
            "/api/candidates",
            $"/api/candidates/{created.Id}",
            $"/api/candidates/{created.Id}/languages",
            $"/api/candidates/{created.Id}/documents",
        })
        {
            var response = await client.DeleteAsync(route);
            Assert.True(
                response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed,
                $"DELETE {route} answered {response.StatusCode}; no candidate route may accept it.");
        }
        // Nothing was destroyed.
        Assert.Equal("Ana", (await ReadAsync(created.Id)).FirstName);
    }

    [Fact]
    public async Task The_runtime_role_cannot_delete_from_the_candidate_tables()
    {
        await ResetAsync();

        await using var connection = new NpgsqlConnection(database.ConnectionString);
        await connection.OpenAsync();

        // "No physical delete" is enforced by PostgreSQL, not only by the absence of a
        // verb: even a defect in the application cannot destroy a candidate.
        await using (var command = new NpgsqlCommand(
            "SELECT has_table_privilege('ktl_runtime', '\"CND_Candidates\"', 'DELETE')",
            connection))
        {
            Assert.False((bool)(await command.ExecuteScalarAsync())!);
        }

        // Nothing in the candidate group may be truncated, relations and documents
        // included — those keep DELETE because replacing a collection really does remove
        // rows, but wholesale destruction is never a legitimate operation.
        foreach (var table in new[]
        {
            "CND_Candidates",
            "CND_CandidateLanguages",
            "CND_CandidatePrograms",
            "CND_CandidateEducation",
            "CND_CandidateExperience",
            "CND_CandidateSkills",
            "CND_Documents",
        })
        {
            await using var command = new NpgsqlCommand(
                $"SELECT has_table_privilege('ktl_runtime', '\"{table}\"', 'TRUNCATE')",
                connection);
            Assert.False((bool)(await command.ExecuteScalarAsync())!, $"ktl_runtime may TRUNCATE {table}.");
        }
    }

    private static object CreatePayload(string firstName, string lastName) => new
    {
        firstName,
        lastName,
        phone = "",
        email = "",
        location = "",
        province = "",
        country = "España",
        availability = "Inmediata",
        status = CandidateStatuses.New,
        source = "Email",
        notes = "",
        receivedAt = (string?)null,
        consentAt = (string?)null,
        reviewDueAt = (string?)null,
    };

    private static object UpdatePayload(string firstName, string lastName, string phone, uint version) => new
    {
        firstName,
        lastName,
        phone,
        email = "",
        location = "",
        province = "",
        country = "España",
        availability = "Inmediata",
        status = CandidateStatuses.New,
        source = "Email",
        notes = "",
        version,
    };

    private static async Task<CandidateResponse> CreateAsync(HttpClient client, string firstName, string lastName)
    {
        var response = await client.PostAsJsonAsync("/api/candidates", CreatePayload(firstName, lastName));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CandidateResponse>())!;
    }

    /// <summary>
    /// Removes the parts that differ by design rather than by disclosure: the per-request
    /// correlation identifier, and the requested path, which echoes back an identifier the
    /// caller supplied and therefore already knows.
    /// </summary>
    private static string Scrub(string body) =>
        System.Text.RegularExpressions.Regex.Replace(
            System.Text.RegularExpressions.Regex.Replace(
                System.Text.RegularExpressions.Regex.Replace(
                    body, "\"correlationId\":\"[^\"]*\"", "\"correlationId\":\"*\""),
                "\"traceId\":\"[^\"]*\"",
                "\"traceId\":\"*\""),
            "/api/candidates/[0-9a-fA-F-]+",
            "/api/candidates/*");

    private async Task ResetAsync()
    {
        await using var dbContext = NewDbContext();
        await DatabaseInitializer.MigrateAsync(dbContext, CancellationToken.None);
        await dbContext.Documents.ExecuteDeleteAsync();
        await dbContext.CandidateLanguages.ExecuteDeleteAsync();
        await dbContext.CandidatePrograms.ExecuteDeleteAsync();
        await dbContext.CandidateEducation.ExecuteDeleteAsync();
        await dbContext.CandidateExperience.ExecuteDeleteAsync();
        await dbContext.CandidateSkills.ExecuteDeleteAsync();
        await dbContext.Candidates.ExecuteDeleteAsync();
        await dbContext.AuditEvents.Where(audit => audit.EventType.StartsWith("candidate.")).ExecuteDeleteAsync();
        await DatabaseInitializer.SeedCatalogsAsync(dbContext, CancellationToken.None);
    }

    private async Task<Candidate> ReadAsync(Guid id)
    {
        await using var dbContext = NewDbContext();
        return await dbContext.Candidates.AsNoTracking().SingleAsync(candidate => candidate.Id == id);
    }

    private async Task<string> StoredDocumentKeyAsync(Guid candidateId)
    {
        await using var dbContext = NewDbContext();
        return await dbContext.Documents.AsNoTracking()
            .Where(document => document.CandidateId == candidateId)
            .Select(document => document.StorageKey)
            .FirstAsync();
    }

    private ApplicationDbContext NewDbContext() => new(
        new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(database.ConnectionString).Options);

    private WebApplicationFactory<Program> CreateFactory(ICurrentActor actor)
    {
        Environment.SetEnvironmentVariable(
            "ConnectionStrings__ApplicationDatabase",
            database.ConnectionString);
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["DevelopmentActor:Enabled"] = "true",
                    ["ConnectionStrings:ApplicationDatabase"] = database.ConnectionString,
                }));
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<ICurrentActor>();
                services.AddScoped(_ => actor);
            });
        });
    }

    private sealed class TestActor(bool authenticated, params string[] permissions) : ICurrentActor
    {
        public static TestActor Full => new(
            true,
            Permissions.CandidatesRead,
            Permissions.CandidatesCreate,
            Permissions.CandidatesUpdate,
            Permissions.CandidatesDelete);

        public static TestActor Reader => new(true, Permissions.CandidatesRead);

        /// <summary>May read and update, but not remove.</summary>
        public static TestActor Editor => new(true, Permissions.CandidatesRead, Permissions.CandidatesUpdate);

        /// <summary>Authenticated, but holds no candidate capability at all.</summary>
        public static TestActor None => new(true);

        public static TestActor Unauthenticated => new(false);

        public string? ExternalKey => authenticated ? "integration-actor" : null;
        public bool IsAuthenticated => authenticated;
        public bool HasPermission(string permission) =>
            authenticated && permissions.Contains(permission, StringComparer.Ordinal);
    }
}
