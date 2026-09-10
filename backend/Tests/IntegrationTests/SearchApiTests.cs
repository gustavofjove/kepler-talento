using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using KeplerTalento.Application.Abstractions.Identity;
using KeplerTalento.Application.Features.Search;
using KeplerTalento.Domain.Candidates;
using KeplerTalento.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace KeplerTalento.Tests.IntegrationTests;

/// <summary>
/// Candidate search and saved searches through the real HTTP → Application → PostgreSQL
/// path.
/// </summary>
/// <remarks>
/// Every filter assertion is made twice: once against the API, and once against
/// <see cref="ReferenceSearchEvaluator"/>, which is a transcription of the browser-side
/// search KTL-10 replaces. "The new query returns what the old one did" is therefore checked
/// against an independent implementation rather than against expectations written from the
/// same understanding that produced the SQL.
/// </remarks>
[Collection(WebHostCollection.Name)]
public sealed class SearchApiTests(PostgreSqlFixture database) : IClassFixture<PostgreSqlFixture>
{
    private const string SearchRoute = "/api/candidates/search";
    private const string PresetRoute = "/api/search-presets";

    public static TheoryData<string, SearchFiltersInput> ParityCases()
    {
        SearchFiltersInput Filters(
            string? text = null,
            string[]? statuses = null,
            (string Value, string Level)[]? skills = null,
            string? skillMode = null,
            (string Value, string Level)[]? languages = null,
            string? languageMode = null,
            (string Value, string Level)[]? programs = null,
            string? programMode = null,
            string? hasCv = null) => new(
            text,
            statuses,
            skills?.Select(pair => (SearchCriterionInput?)new SearchCriterionInput(pair.Value, pair.Level)).ToArray(),
            skillMode,
            languages?.Select(pair => (SearchCriterionInput?)new SearchCriterionInput(pair.Value, pair.Level)).ToArray(),
            languageMode,
            programs?.Select(pair => (SearchCriterionInput?)new SearchCriterionInput(pair.Value, pair.Level)).ToArray(),
            programMode,
            hasCv);

        return new TheoryData<string, SearchFiltersInput>
        {
            { "no filters at all", Filters() },
            { "every status selected", Filters(statuses: [.. CandidateStatuses.All]) },
            { "one status", Filters(statuses: [CandidateStatuses.Available]) },
            { "two statuses", Filters(statuses: [CandidateStatuses.New, CandidateStatuses.Hired]) },
            { "free text on a surname", Filters(text: "Completo") },
            { "free text on an email fragment", Filters(text: "@ejemplo.test") },
            { "free text of the wrong case", Filters(text: "cOmPlEtO") },
            { "free text containing a wildcard", Filters(text: "100%") },
            { "free text containing an underscore", Filters(text: "o _ d") },
            { "free text matching nothing", Filters(text: "no-existe-nadie") },
            {
                "skill value without a level",
                Filters(skills: [(SearchParityFixture.SkillJava, "")])
            },
            {
                "skill value with a level",
                Filters(skills: [(SearchParityFixture.SkillJava, SearchParityFixture.SkillLevelAdvanced)])
            },
            {
                "skill ANY across two values",
                Filters(
                    skills: [(SearchParityFixture.SkillJava, ""), (SearchParityFixture.SkillSql, "")],
                    skillMode: "ANY")
            },
            {
                "skill ALL across two values",
                Filters(
                    skills: [(SearchParityFixture.SkillJava, ""), (SearchParityFixture.SkillPython, "")],
                    skillMode: "ALL")
            },
            {
                "skill ALL with a level nobody holds together",
                Filters(
                    skills:
                    [
                        (SearchParityFixture.SkillJava, SearchParityFixture.SkillLevelBasic),
                        (SearchParityFixture.SkillPython, SearchParityFixture.SkillLevelAdvanced),
                    ],
                    skillMode: "ALL")
            },
            {
                "repeated skill criteria",
                Filters(
                    skills: [(SearchParityFixture.SkillJava, ""), (SearchParityFixture.SkillJava, "")],
                    skillMode: "ALL")
            },
            {
                "unknown skill value",
                Filters(skills: [("Cobol", "")])
            },
            {
                "language ANY",
                Filters(
                    languages:
                    [
                        (SearchParityFixture.LanguageEnglish, SearchParityFixture.LanguageLevelB2),
                        (SearchParityFixture.LanguageFrench, ""),
                    ],
                    languageMode: "ANY")
            },
            {
                "language ALL across separate rows",
                Filters(
                    languages:
                    [
                        (SearchParityFixture.LanguageEnglish, ""),
                        (SearchParityFixture.LanguageFrench, ""),
                    ],
                    languageMode: "ALL")
            },
            {
                "program value with a level",
                Filters(programs: [(SearchParityFixture.ProgramExcel, SearchParityFixture.ProgramLevelHigh)])
            },
            {
                "program ANY",
                Filters(
                    programs: [(SearchParityFixture.ProgramExcel, ""), (SearchParityFixture.ProgramAutoCad, "")],
                    programMode: "ANY")
            },
            { "primary CV present", Filters(hasCv: "yes") },
            { "primary CV absent", Filters(hasCv: "no") },
            {
                "every family combined",
                Filters(
                    text: "@ejemplo.test",
                    statuses: [CandidateStatuses.InProcess, CandidateStatuses.Available],
                    skills: [(SearchParityFixture.SkillJava, "")],
                    skillMode: "ANY",
                    languages: [(SearchParityFixture.LanguageEnglish, "")],
                    languageMode: "ANY",
                    programs: [(SearchParityFixture.ProgramExcel, "")],
                    programMode: "ANY",
                    hasCv: "yes")
            },
            {
                "combined filters that exclude everything",
                Filters(
                    text: "Completo",
                    statuses: [CandidateStatuses.Hired],
                    skills: [(SearchParityFixture.SkillJava, "")])
            },
        };
    }

    [Theory]
    [MemberData(nameof(ParityCases))]
    public async Task Search_matches_the_reference_evaluator(string _, SearchFiltersInput filters)
    {
        var fixture = await SeedAsync();
        using var factory = CreateFactory(TestActor.Reader);
        using var client = factory.CreateClient();

        var page = await SearchAsync(client, filters, pageSize: 100);

        var expected = ReferenceSearchEvaluator.Evaluate(fixture, filters);
        Assert.Equal(expected, page.Items.Select(item => item.CandidateId));
        Assert.Equal(expected.Count, page.TotalCount);
        // A relation join would return a candidate holding the same value at two levels
        // twice. Asserted separately because a wrong count and a duplicate row are different
        // failures with the same cause.
        Assert.Equal(
            page.Items.Select(item => item.CandidateId).Distinct().Count(),
            page.Items.Count);
    }

    [Fact]
    public async Task A_logically_removed_candidate_is_absent_from_every_page_and_from_the_count()
    {
        var fixture = await SeedAsync();
        var removed = fixture.Single(candidate => !candidate.IsActive);
        using var factory = CreateFactory(TestActor.Reader);
        using var client = factory.CreateClient();

        // Filters this candidate satisfies on every count except being present at all.
        var page = await SearchAsync(
            client,
            new SearchFiltersInput(
                "Eliminado",
                null,
                [new SearchCriterionInput(SearchParityFixture.SkillJava, "")],
                "ANY",
                null,
                null,
                null,
                null,
                "yes"),
            pageSize: 100);

        Assert.DoesNotContain(page.Items, item => item.CandidateId == removed.Id);
        Assert.Equal(0, page.TotalCount);
    }

    [Theory]
    [InlineData("Ana", true)]
    [InlineData("Bruno", true)]
    [InlineData("Diego", true)]
    [InlineData("Elena", false)]
    [InlineData("Fermín", false)]
    public async Task Primary_cv_presence_ignores_the_scan_state(string firstName, bool expected)
    {
        // Ana's primary is clean, Bruno's is still pending, Diego's was refused: all three
        // have a CV. Elena has documents but no primary; Fermín has none at all.
        await SeedAsync();
        using var factory = CreateFactory(TestActor.Reader);
        using var client = factory.CreateClient();

        var page = await SearchAsync(
            client,
            new SearchFiltersInput(firstName, null, null, null, null, null, null, null, expected ? "yes" : "no"),
            pageSize: 100);

        var found = Assert.Single(page.Items);
        Assert.Equal(firstName, found.FirstName);
        Assert.Equal(expected, found.HasPrimaryCv);
        Assert.Equal(expected, found.PrimaryCvDocumentId is not null);
    }

    [Fact]
    public async Task A_search_item_carries_only_the_documented_projection()
    {
        await SeedAsync();
        using var factory = CreateFactory(TestActor.Reader);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            SearchRoute,
            new { filters = new { text = "Ana" }, page = 1, pageSize = 25 });
        var body = await response.Content.ReadAsStringAsync();

        using var document = JsonDocument.Parse(body);
        var item = document.RootElement.GetProperty("items")[0];
        Assert.Equal(
            [
                "candidateId",
                "firstName",
                "lastName",
                "phone",
                "email",
                "status",
                "hasPrimaryCv",
                "primaryCvDocumentId",
                "updatedAt",
            ],
            item.EnumerateObject().Select(property => property.Name));
        // Named individually as well, because a renamed field would satisfy the count above
        // while still leaking.
        foreach (var forbidden in new[]
        {
            "notes", "consentAt", "reviewDueAt", "receivedAt", "location", "province",
            "storageKey", "originalFilename", "scanState", "skills", "languages",
            "programs", "documents", "sourceKey",
        })
        {
            Assert.DoesNotContain($"\"{forbidden}\"", body, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task Pages_are_bounded_deterministic_and_non_overlapping()
    {
        var fixture = await SeedAsync();
        using var factory = CreateFactory(TestActor.Reader);
        using var client = factory.CreateClient();
        var expected = ReferenceSearchEvaluator.Evaluate(
            fixture,
            new SearchFiltersInput(null, null, null, null, null, null, null, null, null));

        var first = await SearchAsync(client, new SearchFiltersInput(null, null, null, null, null, null, null, null, null), page: 1, pageSize: 3);
        var second = await SearchAsync(client, new SearchFiltersInput(null, null, null, null, null, null, null, null, null), page: 2, pageSize: 3);

        Assert.Equal(3, first.Items.Count);
        Assert.Equal(expected.Count, first.TotalCount);
        Assert.Equal(expected.Take(3), first.Items.Select(item => item.CandidateId));
        // The fixture contains two candidates sharing an update instant, so this is where a
        // missing identifier tie-breaker would show up as an overlap or an omission.
        Assert.Equal(expected.Skip(3).Take(3), second.Items.Select(item => item.CandidateId));
        Assert.Empty(first.Items.Select(item => item.CandidateId)
            .Intersect(second.Items.Select(item => item.CandidateId)));
    }

    [Fact]
    public async Task An_omitted_page_size_returns_at_most_the_default()
    {
        await SeedAsync();
        using var factory = CreateFactory(TestActor.Reader);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(SearchRoute, new { filters = new { } });
        var page = await ReadPageAsync(response);

        Assert.Equal(1, page.Page);
        Assert.Equal(25, page.PageSize);
    }

    [Theory]
    [InlineData(0, 25)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    public async Task Pagination_outside_its_bounds_is_refused(int page, int pageSize)
    {
        await SeedAsync();
        using var factory = CreateFactory(TestActor.Reader);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            SearchRoute,
            new { filters = new { }, page, pageSize });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task An_unsupported_status_is_refused_with_a_stable_code()
    {
        await SeedAsync();
        using var factory = CreateFactory(TestActor.Reader);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            SearchRoute,
            new { filters = new { statusValues = new[] { "archived" } } });
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(SearchErrors.StatusInvalid, body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Search_fails_closed_without_a_real_actor_and_without_the_permission()
    {
        await SeedAsync();
        foreach (var actor in new[] { TestActor.Unauthenticated, TestActor.None })
        {
            using var factory = CreateFactory(actor);
            using var client = factory.CreateClient();

            var response = await client.PostAsJsonAsync(SearchRoute, new { filters = new { } });
            var body = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.DoesNotContain("candidateId", body, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("totalCount", body, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task Every_preset_route_fails_closed_without_the_permission()
    {
        await SeedAsync();
        using var factory = CreateFactory(TestActor.None);
        using var client = factory.CreateClient();
        var id = Guid.NewGuid();

        var responses = new[]
        {
            await client.GetAsync(PresetRoute),
            await client.PostAsJsonAsync(PresetRoute, new { name = "X", filters = new { } }),
            await client.PutAsJsonAsync($"{PresetRoute}/{id}", new { name = "X", filters = new { } }),
            await client.DeleteAsync($"{PresetRoute}/{id}"),
            await client.PostAsJsonAsync($"{PresetRoute}/{id}/use", new { }),
        };

        Assert.All(responses, response => Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode));
    }

    [Fact]
    public async Task Preset_lifecycle_persists_in_postgresql_and_stays_owner_scoped()
    {
        await SeedAsync();
        using var mineFactory = CreateFactory(TestActor.Reader);
        using var mine = mineFactory.CreateClient();

        var created = await CreatePresetAsync(mine, "Java senior", new { skillCriteria = new[] { new { value = "Java", level = "" } }, skillMode = "ALL" });
        Assert.Null(created.LastUsedAt);

        var listed = await mine.GetFromJsonAsync<List<SearchPresetResponse>>(PresetRoute);
        Assert.Equal(created.Id, Assert.Single(listed!).Id);

        var applied = await ReadPresetAsync(await mine.PostAsJsonAsync($"{PresetRoute}/{created.Id}/use", new { }));
        Assert.NotNull(applied.LastUsedAt);
        Assert.Equal("ALL", applied.Filters.SkillMode);

        var renamed = await ReadPresetAsync(await mine.PutAsJsonAsync(
            $"{PresetRoute}/{created.Id}",
            new { name = "Java junior", filters = new { hasCv = "yes" } }));
        Assert.Equal(created.Id, renamed.Id);
        // Compared at PostgreSQL's microsecond resolution: a timestamptz round-trip drops
        // the sub-microsecond part of a .NET tick, so exact equality would fail on a value
        // that did not change. What matters is that the update did not move it.
        Assert.Equal(created.CreatedAt, renamed.CreatedAt, TimeSpan.FromMicroseconds(1));
        Assert.Equal("Java junior", renamed.Name);

        // The row itself, not just the response: the owner is stored, and never returned.
        await using (var dbContext = NewDbContext())
        {
            var stored = await dbContext.SearchPresets.AsNoTracking().SingleAsync();
            Assert.Equal("integration-actor", stored.OwnerId);
            Assert.Equal("java junior", stored.NormalizedName);
            Assert.Equal(SearchFilterNormalization.FilterSchemaVersion, stored.FilterSchemaVersion);
        }

        using var theirsFactory = CreateFactory(TestActor.OtherReader);
        using var theirs = theirsFactory.CreateClient();
        Assert.Empty((await theirs.GetFromJsonAsync<List<SearchPresetResponse>>(PresetRoute))!);
        // Another owner's identifier answers exactly as an identifier that never existed.
        var crossOwner = await theirs.PostAsJsonAsync($"{PresetRoute}/{created.Id}/use", new { });
        var absent = await theirs.PostAsJsonAsync($"{PresetRoute}/{Guid.NewGuid()}/use", new { });
        Assert.Equal(HttpStatusCode.NotFound, crossOwner.StatusCode);
        Assert.Equal(absent.StatusCode, crossOwner.StatusCode);
        // Compared on what the refusal says, not on the whole envelope: the problem's
        // `instance` echoes the request path, so it differs by the identifier the caller
        // themselves supplied. Everything the server contributes must be identical.
        var (crossCode, crossDetail) = await ReadProblemAsync(crossOwner);
        var (absentCode, absentDetail) = await ReadProblemAsync(absent);
        Assert.Equal(absentCode, crossCode);
        Assert.Equal(absentDetail, crossDetail);

        Assert.Equal(
            HttpStatusCode.NoContent,
            (await mine.DeleteAsync($"{PresetRoute}/{created.Id}")).StatusCode);
        Assert.Empty((await mine.GetFromJsonAsync<List<SearchPresetResponse>>(PresetRoute))!);
    }

    [Fact]
    public async Task The_unique_constraint_decides_concurrent_creations_of_the_same_name()
    {
        await SeedAsync();
        using var factory = CreateFactory(TestActor.Reader);
        using var first = factory.CreateClient();
        using var second = factory.CreateClient();

        var responses = await Task.WhenAll(
            first.PostAsJsonAsync(PresetRoute, new { name = "Simultánea", filters = new { } }),
            second.PostAsJsonAsync(PresetRoute, new { name = "SIMULTÁNEA", filters = new { } }));

        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Created);
        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Conflict);
        await using var dbContext = NewDbContext();
        Assert.Equal(1, await dbContext.SearchPresets.CountAsync());
    }

    [Fact]
    public async Task A_preset_write_with_an_invalid_filter_stores_nothing()
    {
        await SeedAsync();
        using var factory = CreateFactory(TestActor.Reader);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            PresetRoute,
            new { name = "Inválida", filters = new { hasCv = "quizá" } });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await using var dbContext = NewDbContext();
        Assert.Equal(0, await dbContext.SearchPresets.CountAsync());
    }

    [Fact]
    public async Task A_preset_response_never_carries_its_owner()
    {
        await SeedAsync();
        using var factory = CreateFactory(TestActor.Reader);
        using var client = factory.CreateClient();
        await CreatePresetAsync(client, "Privada", new { });

        var body = await (await client.GetAsync(PresetRoute)).Content.ReadAsStringAsync();

        Assert.DoesNotContain("ownerId", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("integration-actor", body, StringComparison.Ordinal);
    }

    private async Task<IReadOnlyList<ParityCandidate>> SeedAsync()
    {
        await using var dbContext = NewDbContext();
        await DatabaseInitializer.MigrateAsync(dbContext, CancellationToken.None);
        // Each test starts from the documented baseline rather than from whatever the
        // previous one left behind.
        await dbContext.SearchPresets.ExecuteDeleteAsync();
        await dbContext.CandidateSkills.ExecuteDeleteAsync();
        await dbContext.CandidateLanguages.ExecuteDeleteAsync();
        await dbContext.CandidatePrograms.ExecuteDeleteAsync();
        await dbContext.Documents.ExecuteDeleteAsync();
        await dbContext.Candidates.ExecuteDeleteAsync();
        return await SearchParityFixture.SeedAsync(dbContext, CancellationToken.None);
    }

    private static async Task<SearchPage<CandidateSearchItem>> SearchAsync(
        HttpClient client,
        SearchFiltersInput filters,
        int page = 1,
        int pageSize = 25) =>
        await ReadPageAsync(await client.PostAsJsonAsync(
            SearchRoute,
            new SearchRequest(filters, page, pageSize)));

    private sealed record SearchRequest(SearchFiltersInput Filters, int Page, int PageSize);

    private static async Task<SearchPage<CandidateSearchItem>> ReadPageAsync(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<SearchPage<CandidateSearchItem>>())!;
    }

    private static async Task<SearchPresetResponse> CreatePresetAsync(
        HttpClient client,
        string name,
        object filters) =>
        await ReadPresetAsync(await client.PostAsJsonAsync(PresetRoute, new { name, filters }));

    private static async Task<SearchPresetResponse> ReadPresetAsync(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<SearchPresetResponse>())!;
    }

    private static async Task<(string Code, string Detail)> ReadProblemAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return (
            document.RootElement.GetProperty("code").GetString() ?? string.Empty,
            document.RootElement.GetProperty("detail").GetString() ?? string.Empty);
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

    private sealed class TestActor(bool authenticated, string? key, params string[] permissions) : ICurrentActor
    {
        public static TestActor Reader => new(true, "integration-actor", Permissions.CandidatesRead);
        public static TestActor OtherReader => new(true, "other-actor", Permissions.CandidatesRead);
        public static TestActor None => new(true, "integration-actor");
        public static TestActor Unauthenticated => new(false, null);

        public string? ExternalKey => key;
        public bool IsAuthenticated => authenticated;
        public bool HasPermission(string permission) =>
            authenticated && permissions.Contains(permission, StringComparer.Ordinal);
    }
}
