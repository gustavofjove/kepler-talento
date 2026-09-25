using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using KeplerTalento.Application.Abstractions.Identity;
using KeplerTalento.Application.Features.Candidates;
using KeplerTalento.Application.Features.Positions;
using KeplerTalento.Domain.Candidates;
using KeplerTalento.Domain.Identity;
using KeplerTalento.Domain.Positions;
using KeplerTalento.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace KeplerTalento.Tests.IntegrationTests;

/// <summary>KTL-30: the five position candidate routes against a real database and host.</summary>
[Collection(WebHostCollection.Name)]
public sealed class PositionCandidateApiTests(PostgreSqlFixture database) : IClassFixture<PostgreSqlFixture>
{
    private const string Issuer = "https://kepler-talento.test/issuer";
    private const string Audience = "kepler-talento-test-api";
    private const string SigningKey = "ktl-30-integration-test-signing-key-000000000000";

    [Fact]
    public async Task A_manager_adds_restages_lists_and_removes_links_with_audit_evidence()
    {
        await ResetAsync(); await AddUserAsync("manager", "rrhh_user");
        var ana = await AddCandidateAsync("Ana", "Centinela");
        await using var factory = CreateFactory(); using var client = await ClientAsync(factory, "manager");
        var position = await CreatePositionAsync(client, "Backend");

        using var added = await client.PostAsJsonAsync($"/api/positions/{position.Id}/candidates", new { candidateId = ana });
        Assert.Equal(HttpStatusCode.Created, added.StatusCode);
        Assert.Equal($"/api/positions/{position.Id}/candidates/{ana}", added.Headers.Location?.OriginalString);
        var link = (await added.Content.ReadFromJsonAsync<PositionCandidateResponse>())!;
        Assert.Equal(("new", "Ana", true), (link.Stage, link.FirstName, link.CandidateIsActive));

        using var restaged = await client.PutAsJsonAsync($"/api/positions/{position.Id}/candidates/{ana}/stage", new { stage = "interview", version = link.Version });
        var changed = (await restaged.Content.ReadFromJsonAsync<PositionCandidateResponse>())!;
        Assert.Equal("interview", changed.Stage);
        Assert.NotEqual(link.Version, changed.Version);

        using var stale = await client.PutAsJsonAsync($"/api/positions/{position.Id}/candidates/{ana}/stage", new { stage = "hired", version = link.Version });
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal(PositionErrors.CandidateConcurrencyConflict, await CodeAsync(stale));

        var forCandidate = (await client.GetFromJsonAsync<List<CandidatePositionResponse>>($"/api/candidates/{ana}/positions"))!;
        Assert.Equal(("Backend", "open", "interview"), (Assert.Single(forCandidate).Title, forCandidate[0].PositionStatus, forCandidate[0].Stage));

        using var removed = await client.DeleteAsync($"/api/positions/{position.Id}/candidates/{ana}");
        Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);
        Assert.Empty((await client.GetFromJsonAsync<List<PositionCandidateResponse>>($"/api/positions/{position.Id}/candidates"))!);
        using var removedAgain = await client.DeleteAsync($"/api/positions/{position.Id}/candidates/{ana}");
        Assert.Equal(PositionErrors.CandidateLinkNotFound, await CodeAsync(removedAgain));

        // Re-adding after a removal works, and the trail names both ids but no stage or name.
        using var readded = await client.PostAsJsonAsync($"/api/positions/{position.Id}/candidates", new { candidateId = ana });
        Assert.Equal(HttpStatusCode.Created, readded.StatusCode);
        await using var db = NewDbContext();
        var subject = PositionCandidate.AuditSubject(position.Id, ana);
        var events = await db.AuditEvents.AsNoTracking().Where(e => e.SubjectId == subject).OrderBy(e => e.CreatedAtUtc).ToListAsync();
        Assert.Equal(
            [PositionAuditEvents.CandidateAdded, PositionAuditEvents.CandidateStageChanged, PositionAuditEvents.CandidateRemoved, PositionAuditEvents.CandidateAdded],
            events.Select(e => e.EventType));
        var serialized = JsonSerializer.Serialize(events);
        Assert.DoesNotContain("interview", serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("Centinela", serialized, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Lists_are_ordered_and_carry_only_the_minimal_projection()
    {
        await ResetAsync(); await AddUserAsync("manager", "rrhh_user");
        var first = await AddCandidateAsync("Primera", "Nueva");
        var second = await AddCandidateAsync("Segunda", "Nueva");
        var hired = await AddCandidateAsync("Tercera", "Contratada");
        await using var factory = CreateFactory(); using var client = await ClientAsync(factory, "manager");
        var open = await CreatePositionAsync(client, "Abierta");
        var closing = await CreatePositionAsync(client, "Cerrada");
        foreach (var id in new[] { hired, first, second }) await AddAsync(client, open.Id, id);
        var hiredLink = (await client.GetFromJsonAsync<List<PositionCandidateResponse>>($"/api/positions/{open.Id}/candidates"))!.Single(l => l.CandidateId == hired);
        (await client.PutAsJsonAsync($"/api/positions/{open.Id}/candidates/{hired}/stage", new { stage = "hired", version = hiredLink.Version })).EnsureSuccessStatusCode();
        await AddAsync(client, closing.Id, first);
        await CloseAsync(client, closing);

        // The position list counts links without naming anyone.
        var positions = (await client.GetFromJsonAsync<PositionPageResponse>("/api/positions?status=all"))!;
        Assert.Equal(3, positions.Items.Single(p => p.Id == open.Id).CandidateCount);
        Assert.Equal(1, positions.Items.Single(p => p.Id == closing.Id).CandidateCount);

        // Stage order first, then newest first: the two `new` links precede `hired`.
        var list = (await client.GetFromJsonAsync<List<PositionCandidateResponse>>($"/api/positions/{open.Id}/candidates"))!;
        Assert.Equal([second, first, hired], list.Select(l => l.CandidateId));

        var forFirst = (await client.GetFromJsonAsync<List<CandidatePositionResponse>>($"/api/candidates/{first}/positions"))!;
        Assert.Equal(["Abierta", "Cerrada"], forFirst.Select(p => p.Title));

        using var raw = await client.GetAsync($"/api/positions/{open.Id}/candidates");
        using var json = JsonDocument.Parse(await raw.Content.ReadAsStringAsync());
        foreach (var item in json.RootElement.EnumerateArray())
            Assert.Equal(["addedAtUtc", "candidateId", "candidateIsActive", "email", "firstName", "hasPrimaryCv", "lastName", "phone", "stage", "updatedAtUtc", "version"], item.EnumerateObject().Select(p => p.Name).Order());
        using var rawCandidate = await client.GetAsync($"/api/candidates/{first}/positions");
        using var candidateJson = JsonDocument.Parse(await rawCandidate.Content.ReadAsStringAsync());
        foreach (var item in candidateJson.RootElement.EnumerateArray())
            Assert.Equal(["addedAtUtc", "positionId", "positionStatus", "stage", "title", "updatedAtUtc", "version"], item.EnumerateObject().Select(p => p.Name).Order());
    }

    [Fact]
    public async Task Preconditions_refuse_closed_positions_removed_candidates_duplicates_and_unknown_ids()
    {
        await ResetAsync(); await AddUserAsync("manager", "rrhh_user");
        var active = await AddCandidateAsync("Activa", "Candidata");
        var removed = await AddCandidateAsync("Eliminada", "Candidata", deactivate: true);
        await using var factory = CreateFactory(); using var client = await ClientAsync(factory, "manager");
        var position = await CreatePositionAsync(client, "Precondiciones");
        await AddAsync(client, position.Id, active);

        await AssertProblemAsync(client.PostAsJsonAsync($"/api/positions/{position.Id}/candidates", new { candidateId = active }), HttpStatusCode.Conflict, PositionErrors.CandidateAlreadyLinked);
        await AssertProblemAsync(client.PostAsJsonAsync($"/api/positions/{position.Id}/candidates", new { candidateId = removed }), HttpStatusCode.Conflict, PositionErrors.CandidateInactive);
        await AssertProblemAsync(client.PostAsJsonAsync($"/api/positions/{position.Id}/candidates", new { candidateId = Guid.CreateVersion7() }), HttpStatusCode.NotFound, CandidateErrors.NotFound);
        await AssertProblemAsync(client.PostAsJsonAsync($"/api/positions/{Guid.CreateVersion7()}/candidates", new { candidateId = active }), HttpStatusCode.NotFound, PositionErrors.NotFound);
        await AssertProblemAsync(client.PostAsJsonAsync($"/api/positions/{position.Id}/candidates", new { candidateId = (Guid?)null }), HttpStatusCode.BadRequest, PositionErrors.CandidateRequired);
        await AssertProblemAsync(client.PutAsJsonAsync($"/api/positions/{position.Id}/candidates/{active}/stage", new { stage = "referred", version = 1 }), HttpStatusCode.BadRequest, PositionErrors.CandidateStageInvalid);
        await AssertProblemAsync(client.GetAsync($"/api/candidates/{Guid.CreateVersion7()}/positions"), HttpStatusCode.NotFound, CandidateErrors.NotFound);
        await AssertProblemAsync(client.GetAsync($"/api/positions/{Guid.CreateVersion7()}/candidates"), HttpStatusCode.NotFound, PositionErrors.NotFound);

        // A link to a candidate removed later stays listed and can still change stage.
        await using (var db = NewDbContext())
        {
            var candidate = await db.Candidates.SingleAsync(c => c.Id == active);
            candidate.Deactivate(DateTimeOffset.UtcNow);
            await db.SaveChangesAsync();
        }
        var link = Assert.Single((await client.GetFromJsonAsync<List<PositionCandidateResponse>>($"/api/positions/{position.Id}/candidates"))!);
        Assert.False(link.CandidateIsActive);
        (await client.PutAsJsonAsync($"/api/positions/{position.Id}/candidates/{active}/stage", new { stage = "rejected", version = link.Version })).EnsureSuccessStatusCode();

        // A closed position is read-only for links until reopened.
        var current = (await client.GetFromJsonAsync<PositionResponse>($"/api/positions/{position.Id}"))!;
        var closed = await CloseAsync(client, current);
        await AssertProblemAsync(client.DeleteAsync($"/api/positions/{position.Id}/candidates/{active}"), HttpStatusCode.Conflict, PositionErrors.CandidatePositionClosed);
        await AssertProblemAsync(client.PutAsJsonAsync($"/api/positions/{position.Id}/candidates/{active}/stage", new { stage = "new", version = 1 }), HttpStatusCode.Conflict, PositionErrors.CandidatePositionClosed);
        Assert.Single((await client.GetFromJsonAsync<List<PositionCandidateResponse>>($"/api/positions/{position.Id}/candidates"))!);
        (await client.PutAsJsonAsync($"/api/positions/{position.Id}", new { title = closed.Title, description = closed.Description, location = closed.Location, status = "open", requirements = closed.Requirements, version = closed.Version })).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/positions/{position.Id}/candidates/{active}")).StatusCode);
    }

    [Fact]
    public async Task Concurrent_adds_of_the_same_candidate_store_one_link()
    {
        await ResetAsync(); await AddUserAsync("manager", "rrhh_user");
        var candidate = await AddCandidateAsync("Carrera", "Concurrente");
        await using var factory = CreateFactory(); using var client = await ClientAsync(factory, "manager");
        var position = await CreatePositionAsync(client, "Carrera");

        var responses = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ =>
            client.PostAsJsonAsync($"/api/positions/{position.Id}/candidates", new { candidateId = candidate })));

        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.Created));
        Assert.All(responses.Where(r => r.StatusCode != HttpStatusCode.Created), r => Assert.Equal(HttpStatusCode.Conflict, r.StatusCode));
        foreach (var response in responses.Where(r => r.StatusCode == HttpStatusCode.Conflict))
            Assert.Equal(PositionErrors.CandidateAlreadyLinked, await CodeAsync(response));
        await using var db = NewDbContext();
        Assert.Equal(1, await db.PositionCandidates.CountAsync());
    }

    [Fact]
    public async Task A_position_at_the_link_limit_refuses_another_add()
    {
        await ResetAsync(); await AddUserAsync("manager", "rrhh_user");
        var extra = await AddCandidateAsync("Sobrante", "Limite");
        await using var factory = CreateFactory(); using var client = await ClientAsync(factory, "manager");
        var position = await CreatePositionAsync(client, "Llena");
        await using (var db = NewDbContext())
        {
            var now = DateTimeOffset.UtcNow;
            for (var index = 0; index < PositionCandidate.MaximumPerPosition; index++)
            {
                var candidate = new Candidate(Guid.CreateVersion7(), $"Relleno{index}", "Limite", now);
                db.Candidates.Add(candidate);
                db.PositionCandidates.Add(new PositionCandidate(Guid.CreateVersion7(), position.Id, candidate.Id, now));
            }
            await db.SaveChangesAsync();
        }

        await AssertProblemAsync(client.PostAsJsonAsync($"/api/positions/{position.Id}/candidates", new { candidateId = extra }), HttpStatusCode.Conflict, PositionErrors.CandidateLimitReached);
        Assert.Equal(PositionCandidate.MaximumPerPosition, (await client.GetFromJsonAsync<List<PositionCandidateResponse>>($"/api/positions/{position.Id}/candidates"))!.Count);
    }

    [Fact]
    public async Task Unauthenticated_and_unauthorized_callers_are_refused_identically_before_validation_or_lookup()
    {
        await ResetAsync();
        await AddRoleAsync("positions_no_candidates", Permissions.PositionsRead, Permissions.PositionsManage);
        await AddRoleAsync("candidates_no_positions", Permissions.CandidatesRead, Permissions.CandidatesUpdate);
        await AddUserAsync("manager", "rrhh_user");
        await AddUserAsync("positionsonly", "positions_no_candidates");
        await AddUserAsync("candidatesonly", "candidates_no_positions");
        await AddUserAsync("reader", "manager_reader");
        var candidate = await AddCandidateAsync("Protegida", "Centinela");
        await using var factory = CreateFactory();
        using var manager = await ClientAsync(factory, "manager");
        var position = await CreatePositionAsync(manager, "Protegida");
        await AddAsync(manager, position.Id, candidate);

        using var anonymous = factory.CreateClient();
        foreach (var (valid, invalid) in RoutePairs(position.Id, candidate))
        {
            using var validResponse = await anonymous.SendAsync(valid());
            using var invalidResponse = await anonymous.SendAsync(invalid());
            Assert.Equal(HttpStatusCode.Unauthorized, validResponse.StatusCode);
            Assert.Equal(validResponse.StatusCode, invalidResponse.StatusCode);
            Assert.Equal(await validResponse.Content.ReadAsStringAsync(), await invalidResponse.Content.ReadAsStringAsync());
        }

        foreach (var name in new[] { "positionsonly", "candidatesonly" })
        {
            using var outsider = await ClientAsync(factory, name);
            foreach (var (valid, invalid) in RoutePairs(position.Id, candidate))
            {
                using var validResponse = await outsider.SendAsync(valid());
                using var invalidResponse = await outsider.SendAsync(invalid());
                Assert.Equal(HttpStatusCode.Forbidden, validResponse.StatusCode);
                Assert.Equal(HttpStatusCode.Forbidden, invalidResponse.StatusCode);
                var body = await validResponse.Content.ReadAsStringAsync();
                Assert.Equal(body, await invalidResponse.Content.ReadAsStringAsync());
                Assert.DoesNotContain("Centinela", body, StringComparison.Ordinal);
            }
        }

        // A reader holding positions.read and candidates.read may read but not write.
        using var reader = await ClientAsync(factory, "reader");
        Assert.Equal(HttpStatusCode.OK, (await reader.GetAsync($"/api/positions/{position.Id}/candidates")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await reader.GetAsync($"/api/candidates/{candidate}/positions")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await reader.DeleteAsync($"/api/positions/{position.Id}/candidates/{candidate}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await reader.PostAsJsonAsync($"/api/positions/{position.Id}/candidates", new { candidateId = candidate })).StatusCode);

        await using var db = NewDbContext();
        Assert.Equal(1, await db.PositionCandidates.CountAsync());
    }

    private static IEnumerable<(Func<HttpRequestMessage> Valid, Func<HttpRequestMessage> Invalid)> RoutePairs(Guid position, Guid candidate)
    {
        var missing = Guid.CreateVersion7();
        yield return (() => new(HttpMethod.Get, $"/api/positions/{position}/candidates"), () => new(HttpMethod.Get, $"/api/positions/{missing}/candidates"));
        yield return (() => new(HttpMethod.Get, $"/api/candidates/{candidate}/positions"), () => new(HttpMethod.Get, $"/api/candidates/{missing}/positions"));
        yield return (
            () => new(HttpMethod.Post, $"/api/positions/{position}/candidates") { Content = JsonContent.Create(new { candidateId = candidate }) },
            () => new(HttpMethod.Post, $"/api/positions/{missing}/candidates") { Content = new StringContent("{not json", System.Text.Encoding.UTF8, "application/json") });
        yield return (
            () => new(HttpMethod.Put, $"/api/positions/{position}/candidates/{candidate}/stage") { Content = JsonContent.Create(new { stage = "interview", version = 1 }) },
            () => new(HttpMethod.Put, $"/api/positions/{missing}/candidates/{missing}/stage") { Content = JsonContent.Create(new { stage = "nope", version = 0 }) });
        yield return (() => new(HttpMethod.Delete, $"/api/positions/{position}/candidates/{candidate}"), () => new(HttpMethod.Delete, $"/api/positions/{missing}/candidates/{missing}"));
    }

    private static async Task AssertProblemAsync(Task<HttpResponseMessage> request, HttpStatusCode status, string code)
    {
        using var response = await request;
        Assert.Equal(status, response.StatusCode);
        Assert.Contains(code, await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    private static async Task AddAsync(HttpClient client, Guid positionId, Guid candidateId)
    {
        using var response = await client.PostAsJsonAsync($"/api/positions/{positionId}/candidates", new { candidateId });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    private static async Task<PositionResponse> CreatePositionAsync(HttpClient client, string title)
    {
        using var response = await client.PostAsJsonAsync("/api/positions", new { title, description = "", location = "", requirements = new { } });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<PositionResponse>())!;
    }

    private static async Task<PositionResponse> CloseAsync(HttpClient client, PositionResponse position)
    {
        using var response = await client.PutAsJsonAsync($"/api/positions/{position.Id}", new { title = position.Title, description = position.Description, location = position.Location, status = "closed", requirements = position.Requirements, version = position.Version });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<PositionResponse>())!;
    }

    private async Task<Guid> AddCandidateAsync(string firstName, string lastName, bool deactivate = false)
    {
        await using var db = NewDbContext();
        var candidate = new Candidate(Guid.CreateVersion7(), firstName, lastName, DateTimeOffset.UtcNow);
        if (deactivate) candidate.Deactivate(DateTimeOffset.UtcNow);
        db.Candidates.Add(candidate);
        await db.SaveChangesAsync();
        return candidate.Id;
    }

    private static async Task<string?> CodeAsync(HttpResponseMessage response)
    {
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.TryGetProperty("code", out var code) ? code.GetString() : null;
    }

    private static async Task<HttpClient> ClientAsync(WebApplicationFactory<Program> factory, string subject)
    {
        var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync("/api/dev/token", new { subject, displayName = "Integration Caller", email = $"{subject}@example.test" });
        response.EnsureSuccessStatusCode();
        var token = (await response.Content.ReadFromJsonAsync<DevelopmentTokenResponse>())!.AccessToken;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private async Task AddRoleAsync(string name, params string[] permissions)
    {
        await using var db = NewDbContext();
        db.Roles.Add(new Role(Guid.CreateVersion7(), name, name, false, permissions, DateTimeOffset.UtcNow));
        await db.SaveChangesAsync();
    }

    private async Task ResetAsync() { await using var db = NewDbContext(); await db.Database.EnsureDeletedAsync(); await DatabaseInitializer.MigrateAsync(db, CancellationToken.None); await DatabaseInitializer.SeedCatalogsAsync(db, CancellationToken.None); }
    private async Task AddUserAsync(string subject, string roleName) { await using var db = NewDbContext(); db.Users.Add(new User(Guid.CreateVersion7(), subject, "Integration User", $"{subject}@example.test", roleName, DateTimeOffset.UtcNow)); await db.SaveChangesAsync(); }
    private ApplicationDbContext NewDbContext() => new(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(database.ConnectionString).Options);
    private WebApplicationFactory<Program> CreateFactory()
    {
        Environment.SetEnvironmentVariable("ConnectionStrings__ApplicationDatabase", database.ConnectionString); Environment.SetEnvironmentVariable("DevelopmentActor__Enabled", "false"); Environment.SetEnvironmentVariable("OperationWorker__Enabled", "false"); Environment.SetEnvironmentVariable("Authentication__SubjectClaim", "oid"); Environment.SetEnvironmentVariable("Authentication__DevelopmentIssuer__Enabled", "true"); Environment.SetEnvironmentVariable("Authentication__DevelopmentIssuer__SigningKey", SigningKey); Environment.SetEnvironmentVariable("Authentication__DevelopmentIssuer__Issuer", Issuer); Environment.SetEnvironmentVariable("Authentication__DevelopmentIssuer__Audience", Audience);
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder => { builder.UseEnvironment("Testing"); builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:ApplicationDatabase"] = database.ConnectionString, ["DevelopmentActor:Enabled"] = "false", ["Authentication:SubjectClaim"] = "oid", ["Authentication:DevelopmentIssuer:Enabled"] = "true", ["Authentication:DevelopmentIssuer:SigningKey"] = SigningKey, ["Authentication:DevelopmentIssuer:Issuer"] = Issuer, ["Authentication:DevelopmentIssuer:Audience"] = Audience, ["ProxyTrust:KnownNetworks:0"] = "127.0.0.0/8" })); });
    }
    private sealed record DevelopmentTokenResponse(string AccessToken);
}
