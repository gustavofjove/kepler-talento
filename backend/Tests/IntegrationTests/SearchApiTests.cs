using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using KeplerTalento.Application.Abstractions.Identity;
using KeplerTalento.Application.Features.Search;
using KeplerTalento.Domain.Candidates;
using KeplerTalento.Domain.Catalogs;
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
/// Candidate search and the shared saved-search library through the real HTTP → Application →
/// PostgreSQL path.
/// </summary>
/// <remarks>
/// Every filter assertion is made twice: once against the API, and once against
/// <see cref="ReferenceSearchEvaluator"/>, an independent in-memory implementation of the
/// current search rules. This checks the SQL against a separate implementation rather than
/// expectations written from the same understanding that produced the query.
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
                "skill minimum includes a higher level",
                Filters(skills: [(SearchParityFixture.SkillJava, SearchParityFixture.SkillLevelBasic)])
            },
            {
                "skill ALL applies independent minimums",
                Filters(skills:
                    [(SearchParityFixture.SkillJava, SearchParityFixture.SkillLevelBasic),
                     (SearchParityFixture.SkillPython, SearchParityFixture.SkillLevelBasic)],
                    skillMode: "ALL")
            },
            {
                "repeated skill value at distinct minimums remains in ALL",
                Filters(skills:
                    [(SearchParityFixture.SkillJava, SearchParityFixture.SkillLevelBasic),
                     (SearchParityFixture.SkillJava, SearchParityFixture.SkillLevelAdvanced)],
                    skillMode: "ALL")
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
                "language minimum includes a higher level",
                Filters(languages: [(SearchParityFixture.LanguageEnglish, SearchParityFixture.LanguageLevelB2)])
            },
            {
                "highest language level matches itself only",
                Filters(languages: [(SearchParityFixture.LanguageEnglish, SearchParityFixture.LanguageLevelC1)])
            },
            {
                "unknown language level matches nothing",
                Filters(languages: [(SearchParityFixture.LanguageEnglish, "Z9")])
            },
            {
                "level from the wrong family matches nothing",
                Filters(languages: [(SearchParityFixture.LanguageEnglish, SearchParityFixture.SkillLevelAdvanced)])
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
                "program minimum includes a higher level",
                Filters(programs: [(SearchParityFixture.ProgramExcel, SearchParityFixture.ProgramLevelMedium)])
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
    public async Task Language_minimum_includes_higher_levels()
    {
        var fixture = await SeedAsync();
        using var factory = CreateFactory(TestActor.Reader);
        using var client = factory.CreateClient();

        SearchFiltersInput At(string level) => new(null, null, null, null,
            [new SearchCriterionInput(SearchParityFixture.LanguageEnglish, level)],
            "ANY", null, null, null);
        var minimum = await SearchAsync(client, At(SearchParityFixture.LanguageLevelB2), pageSize: 100);
        var higher = await SearchAsync(client, At(SearchParityFixture.LanguageLevelC1), pageSize: 100);

        Assert.Equal(["Ana", "Bruno", "Elena", "Fermín"],
            minimum.Items.Select(item => item.FirstName).Order(StringComparer.Ordinal));
        Assert.Equal(["Bruno", "Fermín"],
            higher.Items.Select(item => item.FirstName).Order(StringComparer.Ordinal));
        Assert.Equal(4, minimum.TotalCount);
        Assert.Equal(2, higher.TotalCount);
        Assert.Equal(fixture.Count(candidate => candidate.IsActive &&
            candidate.Languages.Any(language => language.Value == SearchParityFixture.LanguageEnglish)),
            minimum.TotalCount);
    }

    [Fact]
    public async Task Highest_catalog_level_matches_only_itself()
    {
        await SeedAsync();
        await using (var dbContext = NewDbContext())
        {
            var english = await dbContext.CatalogItems.SingleAsync(item => item.Family == CatalogFamilies.Language
                && item.NameNormalized == CatalogName.Normalize(SearchParityFixture.LanguageEnglish));
            var c2 = await dbContext.CatalogItems.SingleAsync(item => item.Family == CatalogFamilies.LanguageLevel
                && item.NameNormalized == CatalogName.Normalize("C2"));
            var candidate = new Candidate(Guid.CreateVersion7(), "Clara", "Highest", DateTimeOffset.UtcNow);
            dbContext.Candidates.Add(candidate);
            dbContext.CandidateLanguages.Add(new CandidateLanguage(Guid.CreateVersion7(), candidate.Id,
                english.Id, c2.Id));
            await dbContext.SaveChangesAsync();
        }
        using var factory = CreateFactory(TestActor.Reader);
        using var client = factory.CreateClient();
        var filters = new SearchFiltersInput(null, null, null, null,
            [new SearchCriterionInput(SearchParityFixture.LanguageEnglish, "C2")],
            "ANY", null, null, null);

        var page = await SearchAsync(client, filters, pageSize: 100);
        Assert.Equal("Clara", Assert.Single(page.Items).FirstName);
        Assert.Equal(1, page.TotalCount);
    }

    [Fact]
    public async Task Inactive_and_reordered_levels_follow_the_configured_rank()
    {
        await SeedAsync();
        await using var dbContext = NewDbContext();
        var b2 = await dbContext.CatalogItems.SingleAsync(item => item.Family == CatalogFamilies.LanguageLevel
            && item.NameNormalized == CatalogName.Normalize(SearchParityFixture.LanguageLevelB2));
        var c1 = await dbContext.CatalogItems.SingleAsync(item => item.Family == CatalogFamilies.LanguageLevel
            && item.NameNormalized == CatalogName.Normalize(SearchParityFixture.LanguageLevelC1));
        var originalB2 = b2.SortOrder;
        var originalC1 = c1.SortOrder;
        var originalActive = c1.IsActive;
        using var factory = CreateFactory(TestActor.Reader);
        using var client = factory.CreateClient();
        var filters = new SearchFiltersInput(null, null, null, null,
            [new SearchCriterionInput(SearchParityFixture.LanguageEnglish, SearchParityFixture.LanguageLevelB2)],
            "ANY", null, null, null);

        try
        {
            c1.SetActive(false, DateTimeOffset.UtcNow);
            await dbContext.SaveChangesAsync();
            var inactive = await SearchAsync(client, filters, pageSize: 100);
            Assert.Contains(inactive.Items, item => item.FirstName == "Bruno");
            Assert.Contains(inactive.Items, item => item.FirstName == "Fermín");

            b2.MoveTo(originalC1, DateTimeOffset.UtcNow);
            c1.MoveTo(originalB2, DateTimeOffset.UtcNow);
            await dbContext.SaveChangesAsync();
            var reordered = await SearchAsync(client, filters, pageSize: 100);
            Assert.Equal(["Ana", "Elena"],
                reordered.Items.Select(item => item.FirstName).Order(StringComparer.Ordinal));
        }
        finally
        {
            b2.MoveTo(originalB2, DateTimeOffset.UtcNow);
            c1.MoveTo(originalC1, DateTimeOffset.UtcNow);
            c1.SetActive(originalActive, DateTimeOffset.UtcNow);
            await dbContext.SaveChangesAsync();
        }
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
                "isActive",
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
    public async Task Tag_search_supports_any_all_empty_unknown_and_level_validation_without_duplicates()
    {
        var seeded = await SeedAsync();
        await using (var db = NewDbContext())
        {
            var tags = await db.CatalogItems
                .Where(item => item.Family == CatalogFamilies.Tag)
                .OrderBy(item => item.SortOrder)
                .Take(2)
                .ToListAsync();
            db.CandidateTags.AddRange(
                new CandidateTag(Guid.NewGuid(), seeded[0].Id, tags[0].Id),
                new CandidateTag(Guid.NewGuid(), seeded[1].Id, tags[0].Id),
                new CandidateTag(Guid.NewGuid(), seeded[1].Id, tags[1].Id),
                new CandidateTag(Guid.NewGuid(), seeded[2].Id, tags[1].Id));
            await db.SaveChangesAsync();
        }
        using var factory = CreateFactory(TestActor.Reader);
        using var client = factory.CreateClient();

        async Task<SearchPage<CandidateSearchItem>> Search(object filters) =>
            await ReadPageAsync(await client.PostAsJsonAsync(
                SearchRoute, new { filters, page = 1, pageSize = 25 }));

        var any = await Search(new
        {
            tagCriteria = new[] { new { value = "Recontratable" }, new { value = "No contactar" } },
            tagMode = "ANY",
        });
        Assert.Equal(3, any.TotalCount);
        Assert.Equal(any.Items.Count, any.Items.Select(item => item.CandidateId).Distinct().Count());

        var all = await Search(new
        {
            tagCriteria = new[] { new { value = "Recontratable" }, new { value = "No contactar" } },
            tagMode = "ALL",
        });
        Assert.Equal(seeded[1].Id, Assert.Single(all.Items).CandidateId);
        Assert.Equal(
            seeded.Count(candidate => candidate.IsActive),
            (await Search(new { tagCriteria = Array.Empty<object>() })).TotalCount);
        Assert.Empty((await Search(new { tagCriteria = new[] { new { value = "Desconocida" } } })).Items);

        var invalid = await client.PostAsJsonAsync(SearchRoute, new
        {
            filters = new { tagCriteria = new[] { new { value = "Recontratable", level = "Alto" } } },
        });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Contains(SearchErrors.TagLevelInvalid, await invalid.Content.ReadAsStringAsync());
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

            // Validation and catalog resolution must not run for a refused caller.
            var invalid = await client.PostAsJsonAsync(SearchRoute, new
            {
                filters = new { languageCriteria = new[] { new { value = "Inglés", level = "SecretLevel" } },
                    tagCriteria = new[] { new { value = "VIP", level = "invalid" } } },
                page = 0,
            });
            Assert.Equal(HttpStatusCode.Forbidden, invalid.StatusCode);
            var invalidBody = await invalid.Content.ReadAsStringAsync();
            Assert.DoesNotContain("SecretLevel", invalidBody, StringComparison.Ordinal);
            Assert.DoesNotContain(SearchErrors.TagLevelInvalid, invalidBody, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task Removed_candidates_are_included_on_request_and_marked_as_removed()
    {
        var fixture = await SeedAsync();
        var removed = fixture.Single(candidate => !candidate.IsActive);
        using var factory = CreateFactory(TestActor.Remover);
        using var client = factory.CreateClient();
        var filters = new SearchFiltersInput(null, null, null, null, null, null, null, null, null);

        var byDefault = await SearchAsync(client, filters, pageSize: 100);
        var included = await ReadPageAsync(await client.PostAsJsonAsync(
            SearchRoute,
            new { filters, page = 1, pageSize = 100, includeInactive = true }));

        Assert.DoesNotContain(byDefault.Items, item => item.CandidateId == removed.Id);
        Assert.All(byDefault.Items, item => Assert.True(item.IsActive));
        Assert.Equal(fixture.Count, included.TotalCount);
        Assert.False(Assert.Single(included.Items, item => item.CandidateId == removed.Id).IsActive);
        Assert.Equal(byDefault.TotalCount + 1, included.TotalCount);
    }

    [Fact]
    public async Task Including_removed_candidates_is_forbidden_for_a_reader_before_validation()
    {
        await SeedAsync();
        using var factory = CreateFactory(TestActor.Reader);
        using var client = factory.CreateClient();

        // Malformed in every other respect, so a 400 would reveal validation ran first.
        var response = await client.PostAsJsonAsync(
            SearchRoute,
            new { filters = new { statusValues = new[] { "archived" } }, page = 0, pageSize = 500, includeInactive = true, sortField = "nope" });
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.DoesNotContain("candidateId", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("totalCount", body, StringComparison.OrdinalIgnoreCase);
    }

    public static TheoryData<string, string> SortCases()
    {
        var data = new TheoryData<string, string>();
        foreach (var field in new[] { "updatedAt", "lastName", "status" })
        {
            foreach (var direction in new[] { "asc", "desc" })
            {
                data.Add(field, direction);
            }
        }
        return data;
    }

    [Theory]
    [MemberData(nameof(SortCases))]
    public async Task Every_sort_pages_through_heavily_tied_data_without_overlap_or_omission(string field, string direction)
    {
        await SeedAsync();
        var tied = await SeedTiedCandidatesAsync(40);
        using var factory = CreateFactory(TestActor.Remover);
        using var client = factory.CreateClient();
        var filters = new { text = TiedMarker };

        var seen = new List<CandidateSearchItem>();
        for (var page = 1; ; page++)
        {
            var result = await ReadPageAsync(await client.PostAsJsonAsync(
                SearchRoute,
                new { filters, page, pageSize = 7, includeInactive = true, sortField = field, sortDirection = direction }));
            Assert.Equal(tied.Count, result.TotalCount);
            if (result.Items.Count == 0)
            {
                break;
            }
            seen.AddRange(result.Items);
        }

        var ids = seen.Select(item => item.CandidateId).ToList();
        Assert.Equal(ids.Count, ids.Distinct().Count());
        Assert.Equal(tied.Order(), ids.Order());
        // Within a run of equal sort values, the identifier ascending decides — in both
        // directions, because the tie-breaker never flips.
        Func<CandidateSearchItem, string> key = field switch
        {
            "lastName" => item => $"{item.LastName}{item.FirstName}",
            "status" => item => item.Status,
            _ => item => item.UpdatedAt.UtcTicks.ToString("D20", System.Globalization.CultureInfo.InvariantCulture),
        };
        for (var index = 1; index < seen.Count; index++)
        {
            if (key(seen[index]) == key(seen[index - 1]))
            {
                // PostgreSQL orders uuid bytewise, which is the ordinal order of the hex text.
                Assert.True(string.CompareOrdinal(
                    seen[index - 1].CandidateId.ToString(),
                    seen[index].CandidateId.ToString()) < 0);
            }
        }
    }

    [Theory]
    [InlineData("email")]
    [InlineData("\"UpdatedAtUtc\"; DROP TABLE \"CND_Candidates\"; --")]
    [InlineData("LastName")]
    public async Task An_unknown_sort_field_is_refused_with_a_stable_code_and_never_reaches_the_query(string sortField)
    {
        await SeedAsync();
        using var factory = CreateFactory(TestActor.Reader);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(SearchRoute, new { filters = new { }, sortField });

        var body = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(SearchErrors.SortFieldInvalid, body, StringComparison.Ordinal);
        // The refusal does not echo the caller's text back.
        Assert.DoesNotContain("DROP TABLE", body, StringComparison.Ordinal);
        await using var dbContext = NewDbContext();
        // The table the injection-shaped value names is still there and still populated.
        Assert.True(await dbContext.Candidates.AnyAsync());
    }

    [Fact]
    public async Task A_page_past_the_end_is_empty_and_reports_the_true_total()
    {
        var fixture = await SeedAsync();
        using var factory = CreateFactory(TestActor.Reader);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(SearchRoute, new { filters = new { }, page = 50, pageSize = 25 });
        var page = await ReadPageAsync(response);

        Assert.Empty(page.Items);
        Assert.Equal(50, page.Page);
        Assert.Equal(fixture.Count(candidate => candidate.IsActive), page.TotalCount);
    }

    [Fact]
    public async Task A_list_response_with_removed_candidates_still_carries_no_personal_detail_beyond_the_projection()
    {
        await SeedAsync();
        using var factory = CreateFactory(TestActor.Remover);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            SearchRoute,
            new { filters = new { }, page = 1, pageSize = 100, includeInactive = true, sortField = "lastName", sortDirection = "asc" });
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        foreach (var forbidden in new[]
        {
            "notes", "consentAt", "reviewDueAt", "receivedAt", "location", "province", "country",
            "source", "availability", "deletedAt", "storageKey", "originalFilename", "scanState",
            "skills", "languages", "programs", "education", "experience", "documents", "sourceKey",
        })
        {
            Assert.DoesNotContain($"\"{forbidden}\"", body, StringComparison.OrdinalIgnoreCase);
        }
    }

    private const string TiedMarker = "EmpateKtl18";

    /// <summary>
    /// Candidates that share an update instant, a status and a surname, so every contracted
    /// sort leaves the identifier as the only thing separating most of them.
    /// </summary>
    private async Task<IReadOnlyList<Guid>> SeedTiedCandidatesAsync(int count)
    {
        await using var dbContext = NewDbContext();
        var instant = new DateTimeOffset(2026, 5, 5, 10, 0, 0, TimeSpan.Zero);
        var ids = new List<Guid>();
        for (var index = 0; index < count; index++)
        {
            var id = Guid.CreateVersion7();
            var candidate = new Candidate(id, index % 2 == 0 ? "Ana" : "Luis", TiedMarker, instant);
            candidate.SetDetails(
                phone: string.Empty,
                email: string.Empty,
                location: string.Empty,
                province: string.Empty,
                country: string.Empty,
                availability: string.Empty,
                status: index % 5 == 0 ? CandidateStatuses.Hired : CandidateStatuses.Available,
                source: string.Empty,
                notes: string.Empty,
                updatedAtUtc: index % 3 == 0 ? instant.AddMinutes(1) : instant);
            dbContext.Candidates.Add(candidate);
            ids.Add(id);
        }
        await dbContext.SaveChangesAsync();
        return ids;
    }

    public static TheoryData<string, bool, bool> PresetAuthorizationMatrix() => new()
    {
        // actor, may read (list, get, use), may write (create, update, delete)
        { nameof(TestActor.Unauthenticated), false, false },
        { nameof(TestActor.None), false, false },
        { nameof(TestActor.Reader), true, false },
        { nameof(TestActor.ManagerOnly), false, true },
    };

    [Theory]
    [MemberData(nameof(PresetAuthorizationMatrix))]
    public async Task Every_preset_route_fails_closed_for_the_permission_it_requires(
        string actorName,
        bool mayRead,
        bool mayWrite)
    {
        await SeedAsync();
        var existing = await CreateAsManagerAsync("Secreta", new { text = "Marta Ruiz" });
        using var factory = CreateFactory(TestActor.Named(actorName));
        using var client = factory.CreateClient();

        // Reads and writes use deliberately invalid payloads for writes, so a refusal that came
        // from validation rather than authorization would show up as a 400.
        var reads = new[]
        {
            await client.GetAsync(PresetRoute),
            await client.GetAsync($"{PresetRoute}/{existing.Id}"),
            await client.PostAsJsonAsync($"{PresetRoute}/{existing.Id}/use", new { }),
        };
        var writes = new[]
        {
            await client.PostAsJsonAsync(PresetRoute, new { name = "", filters = new { hasCv = "quizá" } }),
            await client.PutAsJsonAsync($"{PresetRoute}/{existing.Id}", new { name = "", filters = new { } }),
            await client.DeleteAsync($"{PresetRoute}/{existing.Id}?version=abc"),
        };

        var refused = (mayRead ? Array.Empty<HttpResponseMessage>() : reads)
            .Concat(mayWrite ? Array.Empty<HttpResponseMessage>() : writes);
        foreach (var response in refused)
        {
            var body = await response.Content.ReadAsStringAsync();
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.DoesNotContain("Secreta", body, StringComparison.Ordinal);
            Assert.DoesNotContain("Marta", body, StringComparison.Ordinal);
            Assert.DoesNotContain("validation", body, StringComparison.OrdinalIgnoreCase);
        }
        if (mayRead)
        {
            Assert.All(reads, response => Assert.True(response.IsSuccessStatusCode));
        }
        if (mayWrite)
        {
            // Authorized, so the same invalid payloads now reach validation.
            Assert.All(writes, response => Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode));
        }

        // A refused write changed nothing, and a refused apply recorded no use.
        await using var dbContext = NewDbContext();
        var stored = await dbContext.SearchPresets.AsNoTracking().SingleAsync();
        Assert.Equal("Secreta", stored.Name);
        Assert.Equal(1, stored.Version);
        if (!mayRead)
        {
            Assert.Null(stored.LastUsedAtUtc);
        }
    }

    [Fact]
    public async Task A_preset_created_by_one_actor_is_the_same_preset_for_every_reader()
    {
        await SeedAsync();
        var created = await CreateAsManagerAsync(
            "Java senior",
            new { skillCriteria = new[] { new { value = "Java", level = "" } }, skillMode = "ALL" });

        foreach (var reader in new[] { TestActor.Reader, TestActor.OtherReader })
        {
            using var factory = CreateFactory(reader);
            using var client = factory.CreateClient();

            var listed = await client.GetFromJsonAsync<List<SearchPresetResponse>>(PresetRoute);
            var fetched = await client.GetFromJsonAsync<SearchPresetResponse>($"{PresetRoute}/{created.Id}");

            Assert.Equal(created.Id, Assert.Single(listed!).Id);
            Assert.Equal("Java senior", fetched!.Name);
            Assert.Equal("ALL", fetched.Filters.SkillMode);
            Assert.Equal(created.Version, fetched.Version);
        }
    }

    [Fact]
    public async Task Applying_a_preset_keeps_its_filter_bytes_and_searches_at_or_above_the_minimum()
    {
        await SeedAsync();
        using var factory = CreateFactory(TestActor.Manager);
        using var client = factory.CreateClient();
        var created = await CreatePresetAsync(client, "English minimum", new
        {
            languageCriteria = new[] { new { value = SearchParityFixture.LanguageEnglish,
                level = SearchParityFixture.LanguageLevelB2 } },
            languageMode = "ANY",
        });
        string before;
        await using (var dbContext = NewDbContext())
        {
            before = (await dbContext.SearchPresets.AsNoTracking().SingleAsync()).Filters;
        }

        var applied = await ReadPresetAsync(await client.PostAsJsonAsync(
            $"{PresetRoute}/{created.Id}/use", new { }));
        var page = await SearchAsync(client, applied.Filters, pageSize: 100);

        Assert.Equal(["Ana", "Bruno", "Elena", "Fermín"],
            page.Items.Select(item => item.FirstName).Order(StringComparer.Ordinal));
        await using var afterContext = NewDbContext();
        Assert.Equal(before, (await afterContext.SearchPresets.AsNoTracking().SingleAsync()).Filters);
        Assert.Equal(1, (await afterContext.SearchPresets.AsNoTracking().SingleAsync()).FilterSchemaVersion);
    }

    [Fact]
    public async Task Preset_lifecycle_persists_in_postgresql()
    {
        await SeedAsync();
        using var managerFactory = CreateFactory(TestActor.Manager);
        using var manager = managerFactory.CreateClient();
        using var readerFactory = CreateFactory(TestActor.Reader);
        using var reader = readerFactory.CreateClient();

        var created = await CreatePresetAsync(manager, "Java senior", new { skillMode = "ALL" });
        Assert.Null(created.LastUsedAt);
        Assert.Equal(1u, created.Version);

        var applied = await ReadPresetAsync(await reader.PostAsJsonAsync($"{PresetRoute}/{created.Id}/use", new { }));
        Assert.NotNull(applied.LastUsedAt);
        Assert.Equal("ALL", applied.Filters.SkillMode);
        // Applying is not an edit: neither the update time nor the version moves.
        Assert.Equal(created.UpdatedAt, applied.UpdatedAt, TimeSpan.FromMicroseconds(1));
        Assert.Equal(created.Version, applied.Version);

        var renamed = await ReadPresetAsync(await manager.PutAsJsonAsync(
            $"{PresetRoute}/{created.Id}",
            new { name = "Java junior", filters = new { hasCv = "yes" }, version = created.Version }));
        Assert.Equal(created.Id, renamed.Id);
        // Compared at PostgreSQL's microsecond resolution: a timestamptz round-trip drops
        // the sub-microsecond part of a .NET tick, so exact equality would fail on a value
        // that did not change. What matters is that the update did not move it.
        Assert.Equal(created.CreatedAt, renamed.CreatedAt, TimeSpan.FromMicroseconds(1));
        Assert.Equal("Java junior", renamed.Name);
        Assert.Equal(created.Version + 1, renamed.Version);

        await using (var dbContext = NewDbContext())
        {
            var stored = await dbContext.SearchPresets.AsNoTracking().SingleAsync();
            Assert.Equal("java junior", stored.NormalizedName);
            Assert.Equal(2, stored.Version);
            Assert.Equal(SearchFilterNormalization.FilterSchemaVersion, stored.FilterSchemaVersion);
        }

        Assert.Equal(
            HttpStatusCode.NoContent,
            (await manager.DeleteAsync($"{PresetRoute}/{created.Id}?version={renamed.Version}")).StatusCode);
        Assert.Empty((await reader.GetFromJsonAsync<List<SearchPresetResponse>>(PresetRoute))!);
        var gone = await reader.GetAsync($"{PresetRoute}/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, gone.StatusCode);
        Assert.Equal(SearchErrors.PresetNotFound, (await ReadProblemAsync(gone)).Code);
    }

    [Fact]
    public async Task A_name_differing_only_by_case_or_accents_is_refused()
    {
        await SeedAsync();
        using var factory = CreateFactory(TestActor.Manager);
        using var client = factory.CreateClient();
        await CreatePresetAsync(client, "Inglés B2", new { });
        var other = await CreatePresetAsync(client, "Francés", new { });

        var created = await client.PostAsJsonAsync(PresetRoute, new { name = "ingles b2", filters = new { } });
        var renamed = await client.PutAsJsonAsync(
            $"{PresetRoute}/{other.Id}",
            new { name = "INGLES B2", filters = new { }, version = other.Version });

        foreach (var response in new[] { created, renamed })
        {
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            Assert.Equal(SearchErrors.PresetNameConflict, (await ReadProblemAsync(response)).Code);
        }
        await using var dbContext = NewDbContext();
        Assert.Equal(
            ["Francés", "Inglés B2"],
            await dbContext.SearchPresets.OrderBy(preset => preset.Name).Select(preset => preset.Name).ToListAsync());
    }

    [Fact]
    public async Task A_stale_version_is_refused_on_update_and_delete_and_the_newer_change_remains()
    {
        await SeedAsync();
        using var factory = CreateFactory(TestActor.Manager);
        using var first = factory.CreateClient();
        using var second = factory.CreateClient();
        var loaded = await CreatePresetAsync(first, "Original", new { });

        var saved = await first.PutAsJsonAsync(
            $"{PresetRoute}/{loaded.Id}",
            new { name = "Primera", filters = new { }, version = loaded.Version });
        var staleUpdate = await second.PutAsJsonAsync(
            $"{PresetRoute}/{loaded.Id}",
            new { name = "Segunda", filters = new { }, version = loaded.Version });
        var staleDelete = await second.DeleteAsync($"{PresetRoute}/{loaded.Id}?version={loaded.Version}");

        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        foreach (var response in new[] { staleUpdate, staleDelete })
        {
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            Assert.Equal(SearchErrors.PresetConcurrencyConflict, (await ReadProblemAsync(response)).Code);
        }
        await using var dbContext = NewDbContext();
        var stored = await dbContext.SearchPresets.AsNoTracking().SingleAsync();
        Assert.Equal("Primera", stored.Name);
        Assert.Equal(2, stored.Version);
    }

    [Fact]
    public async Task Applying_a_preset_does_not_make_an_administrators_pending_edit_stale()
    {
        await SeedAsync();
        var loadedByAdministrator = await CreateAsManagerAsync("Compartida", new { });
        using (var readerFactory = CreateFactory(TestActor.Reader))
        using (var reader = readerFactory.CreateClient())
        {
            (await reader.PostAsJsonAsync($"{PresetRoute}/{loadedByAdministrator.Id}/use", new { }))
                .EnsureSuccessStatusCode();
        }

        using var managerFactory = CreateFactory(TestActor.Manager);
        using var manager = managerFactory.CreateClient();
        var saved = await manager.PutAsJsonAsync(
            $"{PresetRoute}/{loadedByAdministrator.Id}",
            new { name = "Compartida v2", filters = new { }, version = loadedByAdministrator.Version });

        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
    }

    [Fact]
    public async Task A_missing_or_malformed_version_is_a_validation_problem_once_authorized()
    {
        await SeedAsync();
        using var factory = CreateFactory(TestActor.Manager);
        using var client = factory.CreateClient();
        var preset = await CreatePresetAsync(client, "Versionada", new { });

        var responses = new[]
        {
            await client.PutAsJsonAsync($"{PresetRoute}/{preset.Id}", new { name = "Sin versión", filters = new { } }),
            await client.DeleteAsync($"{PresetRoute}/{preset.Id}"),
            await client.DeleteAsync($"{PresetRoute}/{preset.Id}?version=abc"),
        };

        foreach (var response in responses)
        {
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Contains(SearchErrors.PresetVersionInvalid, await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        }
        await using var dbContext = NewDbContext();
        Assert.Equal("Versionada", (await dbContext.SearchPresets.AsNoTracking().SingleAsync()).Name);
    }

    [Fact]
    public async Task The_unique_constraint_decides_concurrent_creations_of_the_same_name()
    {
        await SeedAsync();
        using var factory = CreateFactory(TestActor.Manager);
        using var first = factory.CreateClient();
        using var second = factory.CreateClient();

        var responses = await Task.WhenAll(
            first.PostAsJsonAsync(PresetRoute, new { name = "Simultánea", filters = new { } }),
            second.PostAsJsonAsync(PresetRoute, new { name = "SIMULTANEA", filters = new { } }));

        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Created);
        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Conflict);
        await using var dbContext = NewDbContext();
        Assert.Equal(1, await dbContext.SearchPresets.CountAsync());
    }

    [Fact]
    public async Task A_preset_write_with_an_invalid_filter_stores_nothing()
    {
        await SeedAsync();
        using var factory = CreateFactory(TestActor.Manager);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            PresetRoute,
            new { name = "Inválida", filters = new { hasCv = "quizá" } });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await using var dbContext = NewDbContext();
        Assert.Equal(0, await dbContext.SearchPresets.CountAsync());
    }

    [Fact]
    public async Task A_preset_response_never_carries_an_actor_identity()
    {
        await SeedAsync();
        using var factory = CreateFactory(TestActor.Manager);
        using var client = factory.CreateClient();
        var created = await CreatePresetAsync(client, "Compartida", new { });

        var bodies = new[]
        {
            await (await client.GetAsync(PresetRoute)).Content.ReadAsStringAsync(),
            await (await client.GetAsync($"{PresetRoute}/{created.Id}")).Content.ReadAsStringAsync(),
        };

        foreach (var body in bodies)
        {
            Assert.DoesNotContain("owner", body, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("integration-admin", body, StringComparison.Ordinal);
        }
    }

    private async Task<IReadOnlyList<ParityCandidate>> SeedAsync()
    {
        await using var dbContext = NewDbContext();
        await DatabaseInitializer.MigrateAsync(dbContext, CancellationToken.None);
        await DatabaseInitializer.SeedCatalogsAsync(dbContext, CancellationToken.None);
        // Each test starts from the documented baseline rather than from whatever the
        // previous one left behind.
        await dbContext.SearchPresets.ExecuteDeleteAsync();
        await dbContext.CandidateNotes.ExecuteDeleteAsync();
        await dbContext.CandidateTags.ExecuteDeleteAsync();
        await dbContext.CandidateSkills.ExecuteDeleteAsync();
        await dbContext.CandidateLanguages.ExecuteDeleteAsync();
        await dbContext.CandidatePrograms.ExecuteDeleteAsync();
        await dbContext.Documents.ExecuteDeleteAsync();
        await dbContext.Candidates.ExecuteDeleteAsync();
        return await SearchParityFixture.SeedAsync(dbContext, CancellationToken.None);
    }

    private async Task<SearchPresetResponse> CreateAsManagerAsync(string name, object filters)
    {
        using var factory = CreateFactory(TestActor.Manager);
        using var client = factory.CreateClient();
        return await CreatePresetAsync(client, name, filters);
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
        // Program selects DevelopmentActor vs token identity while composing services, before
        // WebApplicationFactory's in-memory override is applied.
        Environment.SetEnvironmentVariable("DevelopmentActor__Enabled", "true");
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
        public static TestActor Remover =>
            new(true, "integration-remover", Permissions.CandidatesRead, Permissions.CandidatesDelete);
        public static TestActor OtherReader => new(true, "other-actor", Permissions.CandidatesRead);
        public static TestActor Manager =>
            new(true, "integration-admin", Permissions.CandidatesRead, Permissions.PresetsManage);
        public static TestActor ManagerOnly => new(true, "integration-admin", Permissions.PresetsManage);
        public static TestActor None => new(true, "integration-actor");
        public static TestActor Unauthenticated => new(false, null);

        /// <summary>Lets theory data name an actor, since xUnit data must be serializable.</summary>
        public static TestActor Named(string name) => name switch
        {
            nameof(Reader) => Reader,
            nameof(Manager) => Manager,
            nameof(ManagerOnly) => ManagerOnly,
            nameof(None) => None,
            nameof(Unauthenticated) => Unauthenticated,
            _ => throw new ArgumentOutOfRangeException(nameof(name), name, null),
        };

        public string? ExternalKey => key;

        /// <summary>No stored user stands behind a test double; nothing under test reads it.</summary>
        public Guid? UserId => null;

        public bool IsAuthenticated => authenticated;
        public bool HasPermission(string permission) =>
            authenticated && permissions.Contains(permission, StringComparer.Ordinal);
    }
}
