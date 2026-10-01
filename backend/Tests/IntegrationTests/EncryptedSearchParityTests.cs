using System.Net.Http.Json;
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
/// Text matching and last-name ordering, pinned to what PostgreSQL did before KTL-33 moved
/// both into the API.
/// </summary>
/// <remarks>
/// The expectations are written out rather than computed, and were first proven against the
/// SQL implementation. They encode the measured database behavior recorded in
/// <c>docs/ktl-33/design-notes.md</c>: code-point ordering (upper case before lower case,
/// accented letters after <c>z</c>), case-insensitive but accent-sensitive matching, literal
/// <c>%</c> and <c>_</c>, and U+0130 folding to <c>i</c>. If the implementation changes and any
/// of these move, a user sees a different result list, which is what this test exists to catch.
/// </remarks>
[Collection(WebHostCollection.Name)]
public sealed class EncryptedSearchParityTests(PostgreSqlFixture database) : IClassFixture<PostgreSqlFixture>
{
    private const string SearchRoute = "/api/candidates/search";

    // Seed order is the identifier order (version 7 identifiers ascend), so ties on name, if
    // any, resolve in this order.
    private static readonly (string Key, string First, string Last, string Email, string Phone, string Notes)[] People =
    [
        ("alvaro-avila", "Álvaro", "Ávila", "alvaro@parity.test", "", ""),
        ("alvaro-lower", "alvaro", "avila", "a2@parity.test", "", "100% disponible"),
        ("angel-upper", "ÁNGEL", "MUÑOZ", "angel@parity.test", "", ""),
        ("angel-title", "Ángel", "Muñoz", "a4@parity.test", "", "o_d nota"),
        ("zoe", "Zoe", "Zapata", "z@parity.test", "+34 600 111 222", ""),
        ("nuria", "Nuria", "Nuñez", "n@parity.test", "", ""),
        ("ilkay", "İlkay", "Öztürk", "ozturk@parity.test", "", ""),
        ("ana-torre", "Ana", "de la Torre", "ana@parity.test", "", ""),
        ("luis", "Luis", "De Soto", "luis@parity.test", "", ""),
        ("ana-avila", "Ana", "Ávila", "ana.avila@parity.test", "", ""),
    ];

    public static TheoryData<string, string[]> TextCases() => new()
    {
        { "ángel", ["angel-upper", "angel-title"] },
        { "angel", ["angel-upper"] },
        { "ÁVILA", ["alvaro-avila", "ana-avila"] },
        { "avila", ["alvaro-lower", "ana-avila"] },
        { "100%", ["alvaro-lower"] },
        { "%", ["alvaro-lower"] },
        { "o_d", ["angel-title"] },
        { "_", ["angel-title"] },
        { "ilkay", ["ilkay"] },
        { "de ", ["ana-torre", "luis"] },
        { "600 111", ["zoe"] },
        { "straße", [] },
    };

    [Theory]
    [MemberData(nameof(TextCases))]
    public async Task Text_matching_keeps_its_case_accent_and_wildcard_behavior(string text, string[] expected)
    {
        var ids = await SeedAsync();
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var page = await SearchAsync(client, text, "updatedAt", "desc");

        Assert.Equal(
            expected.Select(key => ids[key]).Order(),
            page.Items.Select(item => item.CandidateId).Order());
        Assert.Equal(expected.Length, page.TotalCount);
    }

    [Theory]
    [InlineData("asc", new[] { "luis", "angel-upper", "angel-title", "nuria", "zoe", "alvaro-lower", "ana-torre", "ana-avila", "alvaro-avila", "ilkay" })]
    [InlineData("desc", new[] { "ilkay", "alvaro-avila", "ana-avila", "ana-torre", "alvaro-lower", "zoe", "nuria", "angel-title", "angel-upper", "luis" })]
    public async Task Last_name_ordering_keeps_code_point_order_across_pages(string direction, string[] expected)
    {
        var ids = await SeedAsync();
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var seen = new List<Guid>();
        for (var page = 1; ; page++)
        {
            var result = await SearchAsync(client, string.Empty, "lastName", direction, page, pageSize: 3);
            Assert.Equal(People.Length, result.TotalCount);
            if (result.Items.Count == 0)
            {
                break;
            }
            seen.AddRange(result.Items.Select(item => item.CandidateId));
        }

        Assert.Equal(expected.Select(key => ids[key]), seen);
    }

    [Fact]
    public async Task Text_search_sorted_by_last_name_combines_both()
    {
        var ids = await SeedAsync();
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var page = await SearchAsync(client, "@parity.test", "lastName", "asc", pageSize: 4);

        Assert.Equal(People.Length, page.TotalCount);
        Assert.Equal(
            new[] { "luis", "angel-upper", "angel-title", "nuria" }.Select(key => ids[key]),
            page.Items.Select(item => item.CandidateId));
    }

    private async Task<IReadOnlyDictionary<string, Guid>> SeedAsync()
    {
        await using var dbContext = NewDbContext();
        await DatabaseInitializer.MigrateAsync(dbContext, CancellationToken.None);
        await DatabaseInitializer.SeedCatalogsAsync(dbContext, CancellationToken.None);
        await dbContext.SearchPresets.ExecuteDeleteAsync();
        await dbContext.PositionCandidates.ExecuteDeleteAsync();
        await dbContext.CandidateNotes.ExecuteDeleteAsync();
        await dbContext.CandidateTags.ExecuteDeleteAsync();
        await dbContext.CandidateSkills.ExecuteDeleteAsync();
        await dbContext.CandidateLanguages.ExecuteDeleteAsync();
        await dbContext.CandidatePrograms.ExecuteDeleteAsync();
        await dbContext.CandidateEducation.ExecuteDeleteAsync();
        await dbContext.CandidateExperience.ExecuteDeleteAsync();
        await dbContext.Documents.ExecuteDeleteAsync();
        await dbContext.Candidates.ExecuteDeleteAsync();

        var instant = new DateTimeOffset(2026, 9, 1, 9, 0, 0, TimeSpan.Zero);
        var ids = new Dictionary<string, Guid>(StringComparer.Ordinal);
        for (var index = 0; index < People.Length; index++)
        {
            var person = People[index];
            var id = Guid.CreateVersion7();
            var candidate = new Candidate(id, person.First, person.Last, instant);
            candidate.SetDetails(
                phone: person.Phone,
                email: person.Email,
                location: string.Empty,
                province: string.Empty,
                country: string.Empty,
                source: string.Empty,
                notes: person.Notes,
                updatedAtUtc: instant.AddMinutes(index));
            dbContext.Candidates.Add(candidate);
            ids[person.Key] = id;
            // Version 7 identifiers are only ordered across milliseconds.
            await Task.Delay(2);
        }
        await dbContext.SaveChangesAsync();
        return ids;
    }

    private static async Task<SearchPage<CandidateSearchItem>> SearchAsync(
        HttpClient client,
        string text,
        string sortField,
        string sortDirection,
        int page = 1,
        int pageSize = 100)
    {
        var response = await client.PostAsJsonAsync(
            SearchRoute,
            new { filters = new { text }, page, pageSize, sortField, sortDirection });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<SearchPage<CandidateSearchItem>>())!;
    }

    private ApplicationDbContext NewDbContext() => new(
        new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(database.ConnectionString).UseTestFieldEncryption().Options);

    private WebApplicationFactory<Program> CreateFactory()
    {
        Environment.SetEnvironmentVariable("ConnectionStrings__ApplicationDatabase", database.ConnectionString);
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
                services.AddScoped<ICurrentActor>(_ => new Reader());
            });
        });
    }

    private sealed class Reader : ICurrentActor
    {
        public string? ExternalKey => "parity-reader";
        public Guid? UserId => null;
        public bool IsAuthenticated => true;
        public bool HasPermission(string permission) =>
            string.Equals(permission, Permissions.CandidatesRead, StringComparison.Ordinal);
    }
}
