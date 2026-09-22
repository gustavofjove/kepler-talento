using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using KeplerTalento.Application.Abstractions.Identity;
using KeplerTalento.Application.Features.Positions;
using KeplerTalento.Domain.Identity;
using KeplerTalento.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;
using Xunit;

namespace KeplerTalento.Tests.IntegrationTests;

[Collection(WebHostCollection.Name)]
public sealed class PositionApiTests(PostgreSqlFixture database) : IClassFixture<PostgreSqlFixture>
{
    private const string Issuer = "https://kepler-talento.test/issuer";
    private const string Audience = "kepler-talento-test-api";
    private const string SigningKey = "ktl-15-integration-test-signing-key-000000000000";

    [Fact]
    public async Task Position_crud_sanitizes_html_pages_lists_and_uses_concurrency()
    {
        await ResetAsync(); await AddUserAsync("manager", "manager@example.test", "rrhh_user");
        await using var factory = CreateFactory(); using var client = factory.CreateClient(); await AuthenticateAsync(client, "manager", "manager@example.test");
        using var createdResponse = await client.PostAsJsonAsync("/api/positions", new { title = "Programador sénior", description = "<p onclick='x'><strong>Hola</strong><script>x</script></p>", location = "Madrid", requirements = new { } });
        Assert.Equal(HttpStatusCode.Created, createdResponse.StatusCode);
        var created = (await createdResponse.Content.ReadFromJsonAsync<PositionResponse>())!;
        Assert.Equal("<p><strong>Hola</strong></p>", created.Description);
        using var listResponse = await client.GetAsync("/api/positions");
        var page = (await listResponse.Content.ReadFromJsonAsync<PositionPageResponse>())!;
        Assert.Single(page.Items); Assert.Equal(25, page.PageSize);
        Assert.DoesNotContain("description", await listResponse.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
        using var updatedResponse = await client.PutAsJsonAsync($"/api/positions/{created.Id}", new { title = created.Title, description = created.Description, location = created.Location, status = "closed", requirements = created.Requirements, version = created.Version });
        updatedResponse.EnsureSuccessStatusCode();
        using var stale = await client.PutAsJsonAsync($"/api/positions/{created.Id}", new { title = created.Title, description = "", location = "", status = "open", requirements = created.Requirements, version = created.Version });
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        using var deleted = await client.DeleteAsync($"/api/positions/{created.Id}");
        Assert.Equal(HttpStatusCode.MethodNotAllowed, deleted.StatusCode);
    }

    [Fact]
    public async Task Position_authorization_precedes_validation_and_existence_disclosure()
    {
        await ResetAsync(); await AddUserAsync("reader", "reader@example.test", "readonly");
        await using var factory = CreateFactory(); using var anonymous = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/positions?status=nope&page=0")).StatusCode);
        using var reader = factory.CreateClient(); await AuthenticateAsync(reader, "reader", "reader@example.test");
        using var refused = await reader.PostAsJsonAsync("/api/positions", new { title = "", description = "", location = "", requirements = new { } });
        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
    }

    [Fact]
    public async Task Migration_seeds_exact_permissions_and_runtime_grants_without_delete()
    {
        await ResetAsync(); await using var db = NewDbContext();
        var roles = await db.Roles.AsNoTracking().ToListAsync();
        Assert.All(roles.Where(role => role.IsSystem), role => Assert.Contains("positions.read", role.Permissions));
        Assert.Equal(["rrhh_admin", "rrhh_user"], roles.Where(role => role.Permissions.Contains("positions.manage")).Select(role => role.Name).Order().ToArray());
        await using var connection = new NpgsqlConnection(database.ConnectionString); await connection.OpenAsync();
        await using var command = new NpgsqlCommand("SELECT has_table_privilege('ktl_runtime', '\"OPS_Positions\"', 'SELECT,INSERT,UPDATE'), has_table_privilege('ktl_runtime', '\"OPS_Positions\"', 'DELETE')", connection);
        await using var reader = await command.ExecuteReaderAsync(); Assert.True(await reader.ReadAsync()); Assert.True(reader.GetBoolean(0)); Assert.False(reader.GetBoolean(1));
    }

    // ---- 5.2: contract ----

    [Fact]
    public async Task Create_returns_an_open_position_and_identifies_the_new_resource()
    {
        await ResetAsync(); await AddUserAsync("manager", "manager@example.test", "rrhh_user");
        await using var factory = CreateFactory(); using var client = await ClientAsync(factory, "manager");

        using var response = await client.PostAsJsonAsync("/api/positions", new { title = "  Analista QA ", description = "", location = " Bilbao ", requirements = new { skillCriteria = new[] { new { value = " Java ", level = "" } }, skillMode = "all" } });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = (await response.Content.ReadFromJsonAsync<PositionResponse>())!;
        Assert.Equal($"/api/positions/{created.Id}", response.Headers.Location?.OriginalString);
        Assert.Equal("open", created.Status);
        Assert.Equal("Analista QA", created.Title);
        Assert.Equal("Bilbao", created.Location);
        Assert.NotEqual(0u, created.Version);
        var read = (await client.GetFromJsonAsync<PositionResponse>($"/api/positions/{created.Id}"))!;
        Assert.Equal("ALL", read.Requirements.SkillMode);
        Assert.Equal("Java", Assert.Single(read.Requirements.SkillCriteria!)!.Value);
    }

    [Fact]
    public async Task Titles_differing_only_by_case_and_accents_conflict_on_create_and_rename()
    {
        await ResetAsync(); await AddUserAsync("manager", "manager@example.test", "rrhh_user");
        await using var factory = CreateFactory(); using var client = await ClientAsync(factory, "manager");
        await CreateAsync(client, "Programador sénior");
        var other = await CreateAsync(client, "Analista");

        using var duplicate = await client.PostAsJsonAsync("/api/positions", Body("programador SENIOR"));
        using var rename = await client.PutAsJsonAsync($"/api/positions/{other.Id}", UpdateBody(other, title: "PROGRAMADOR senior"));

        foreach (var refused in new[] { duplicate, rename })
        {
            Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
            Assert.Equal(PositionErrors.TitleConflict, await CodeAsync(refused));
        }
        Assert.Equal("Analista", (await client.GetFromJsonAsync<PositionResponse>($"/api/positions/{other.Id}"))!.Title);
        Assert.Equal(2, (await ListAsync(client, "?status=all")).TotalCount);
    }

    [Fact]
    public async Task Closing_and_reopening_keep_the_position_and_its_requirements()
    {
        await ResetAsync(); await AddUserAsync("manager", "manager@example.test", "rrhh_user");
        await using var factory = CreateFactory(); using var client = await ClientAsync(factory, "manager");
        var created = await CreateAsync(client, "Backend", requirements: new { text = "java", hasCv = "yes" });

        using var closeResponse = await client.PutAsJsonAsync($"/api/positions/{created.Id}", UpdateBody(created, status: "closed"));
        var closed = (await closeResponse.Content.ReadFromJsonAsync<PositionResponse>())!;
        Assert.Equal("closed", closed.Status);
        Assert.Equal(System.Text.Json.JsonSerializer.Serialize(created.Requirements), System.Text.Json.JsonSerializer.Serialize(closed.Requirements));
        Assert.Empty((await ListAsync(client, string.Empty)).Items);
        Assert.Single((await ListAsync(client, "?status=closed")).Items);

        using var reopenResponse = await client.PutAsJsonAsync($"/api/positions/{created.Id}", UpdateBody(closed, status: "open"));
        Assert.Equal("open", (await reopenResponse.Content.ReadFromJsonAsync<PositionResponse>())!.Status);
        Assert.Equal(created.Id, Assert.Single((await ListAsync(client, string.Empty)).Items).Id);
    }

    [Fact]
    public async Task The_list_filters_sorts_pages_and_returns_only_the_minimal_projection()
    {
        await ResetAsync(); await AddUserAsync("manager", "manager@example.test", "rrhh_user");
        await using var factory = CreateFactory(); using var client = await ClientAsync(factory, "manager");
        var malaga = await CreateAsync(client, "Diseñador", location: "Málaga");
        var zaragoza = await CreateAsync(client, "Arquitecto", location: "Zaragoza");
        var closedMalaga = await CreateAsync(client, "Contable", location: "MALAGA centro");
        await client.PutAsJsonAsync($"/api/positions/{closedMalaga.Id}", UpdateBody(closedMalaga, status: "closed"));

        Assert.Equal([closedMalaga.Id], (await ListAsync(client, "?status=closed&text=malaga")).Items.Select(i => i.Id));
        Assert.Equal([malaga.Id], (await ListAsync(client, "?text=M%C3%81LAGA")).Items.Select(i => i.Id));
        Assert.Equal([malaga.Id], (await ListAsync(client, "?text=disenador")).Items.Select(i => i.Id));
        Assert.Equal(3, (await ListAsync(client, "?status=all&text=%20%20")).TotalCount);
        Assert.Equal(["Arquitecto", "Contable", "Diseñador"], (await ListAsync(client, "?status=all&sortField=title&sortDirection=asc")).Items.Select(i => i.Title));
        Assert.Equal(["Zaragoza", "Málaga"], (await ListAsync(client, "?sortField=location&sortDirection=desc")).Items.Select(i => i.Location));
        Assert.Equal(zaragoza.Id, (await ListAsync(client, string.Empty)).Items[0].Id);

        var pageTwo = await ListAsync(client, "?status=all&pageSize=2&page=2&sortField=title&sortDirection=asc");
        Assert.Equal(["Diseñador"], pageTwo.Items.Select(i => i.Title));
        var pastEnd = await ListAsync(client, "?status=all&page=9&pageSize=100");
        Assert.Empty(pastEnd.Items);
        Assert.Equal(3, pastEnd.TotalCount);
        Assert.Equal(9, pastEnd.Page);

        using var raw = await client.GetAsync("/api/positions?status=all");
        using var json = System.Text.Json.JsonDocument.Parse(await raw.Content.ReadAsStringAsync());
        Assert.Equal(["items", "page", "pageSize", "totalCount"], json.RootElement.EnumerateObject().Select(p => p.Name).Order());
        foreach (var item in json.RootElement.GetProperty("items").EnumerateArray())
        {
            Assert.Equal(["id", "location", "status", "title", "updatedAtUtc", "version"], item.EnumerateObject().Select(p => p.Name).Order());
        }
    }

    [Theory]
    [InlineData("?status=archived", PositionErrors.StatusInvalid)]
    [InlineData("?page=0", PositionErrors.PageInvalid)]
    [InlineData("?pageSize=0", PositionErrors.PageSizeInvalid)]
    [InlineData("?pageSize=101", PositionErrors.PageSizeInvalid)]
    [InlineData("?sortField=description", PositionErrors.SortFieldInvalid)]
    [InlineData("?sortDirection=up", PositionErrors.SortDirectionInvalid)]
    public async Task Unsupported_list_input_is_a_stable_validation_problem(string query, string code)
    {
        await ResetAsync(); await AddUserAsync("reader", "reader@example.test", "readonly");
        await using var factory = CreateFactory(); using var client = await ClientAsync(factory, "reader");

        using var response = await client.GetAsync($"/api/positions{query}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(code, await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Invalid_writes_are_rejected_and_store_nothing()
    {
        await ResetAsync(); await AddUserAsync("manager", "manager@example.test", "rrhh_user");
        await using var factory = CreateFactory(); using var client = await ClientAsync(factory, "manager");
        var created = await CreateAsync(client, "Estable");

        var refusals = new (HttpResponseMessage Response, string Code)[]
        {
            (await client.PutAsJsonAsync($"/api/positions/{created.Id}", UpdateBody(created, status: "archived")), PositionErrors.StatusInvalid),
            (await client.PostAsJsonAsync("/api/positions", Body("   ")), PositionErrors.TitleRequired),
            (await client.PostAsJsonAsync("/api/positions", Body(new string('t', 201))), PositionErrors.TitleTooLong),
            (await client.PostAsJsonAsync("/api/positions", Body("Larga", description: "<p>" + new string('d', 20_001) + "</p>")), PositionErrors.DescriptionTooLong),
            (await client.PostAsJsonAsync("/api/positions", Body("Filtro", requirements: new { hasCv = "maybe" })), "search."),
            (await client.PostAsJsonAsync("/api/positions", Body("Estados", requirements: new { statusValues = new[] { "nope" } })), "search."),
            (await client.PostAsJsonAsync("/api/positions", Body("Modo", requirements: new { skillMode = "SOME" })), "search."),
        };

        foreach (var (response, code) in refusals)
        {
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Contains(code, await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
            response.Dispose();
        }
        var list = await ListAsync(client, "?status=all");
        Assert.Equal(created.Id, Assert.Single(list.Items).Id);
        Assert.Equal("open", list.Items[0].Status);
    }

    [Fact]
    public async Task Active_markup_is_stripped_and_a_missing_position_is_not_found()
    {
        await ResetAsync(); await AddUserAsync("manager", "manager@example.test", "rrhh_user");
        await using var factory = CreateFactory(); using var client = await ClientAsync(factory, "manager");

        var created = await CreateAsync(client, "Segura", description:
            "<p style='color:red'>Uno <a href='javascript:x'>enlace</a><img src=x onerror=alert(1)></p><ul><li><em>dos</em></li></ul><iframe src='//x'></iframe><svg onload=x></svg>");
        using var missing = await client.GetAsync($"/api/positions/{Guid.CreateVersion7()}");
        using var missingUpdate = await client.PutAsJsonAsync($"/api/positions/{Guid.CreateVersion7()}", UpdateBody(created));

        Assert.DoesNotContain("<a", created.Description, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<img", created.Description, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("style", created.Description, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("iframe", created.Description, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("svg", created.Description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("<ul><li><em>dos</em></li></ul>", created.Description, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal(PositionErrors.NotFound, await CodeAsync(missing));
        Assert.Equal(HttpStatusCode.NotFound, missingUpdate.StatusCode);
    }

    [Fact]
    public async Task Writes_record_identifier_only_audit_events()
    {
        await ResetAsync(); await AddUserAsync("manager", "manager@example.test", "rrhh_user");
        await using var factory = CreateFactory(); using var client = await ClientAsync(factory, "manager");
        var created = await CreateAsync(client, "Auditada Centinela", location: "Lugar Centinela", description: "<p>Texto Centinela</p>");
        await client.PutAsJsonAsync($"/api/positions/{created.Id}", UpdateBody(created, status: "closed"));

        await using var db = NewDbContext();
        var events = await db.AuditEvents.AsNoTracking().Where(e => e.SubjectId == created.Id.ToString("N")).OrderBy(e => e.CreatedAtUtc).ToListAsync();
        Assert.Equal([Domain.Positions.PositionAuditEvents.Created, Domain.Positions.PositionAuditEvents.StatusChanged], events.Select(e => e.EventType));
        Assert.DoesNotContain("Centinela", System.Text.Json.JsonSerializer.Serialize(events), StringComparison.Ordinal);
    }

    // ---- 5.3 / 8.2: authorization ----

    [Fact]
    public async Task Unauthenticated_callers_get_the_same_refusal_for_valid_and_invalid_input_on_every_route()
    {
        await ResetAsync(); await AddUserAsync("manager", "manager@example.test", "rrhh_user");
        await using var factory = CreateFactory();
        using var manager = await ClientAsync(factory, "manager");
        var existing = await CreateAsync(manager, "Existente");
        using var anonymous = factory.CreateClient();

        foreach (var (valid, invalid) in RoutePairs(existing))
        {
            using var validResponse = await anonymous.SendAsync(valid());
            using var invalidResponse = await anonymous.SendAsync(invalid());
            Assert.Equal(HttpStatusCode.Unauthorized, validResponse.StatusCode);
            Assert.Equal(validResponse.StatusCode, invalidResponse.StatusCode);
            Assert.Equal(await validResponse.Content.ReadAsStringAsync(), await invalidResponse.Content.ReadAsStringAsync());
        }
    }

    [Fact]
    public async Task Callers_without_the_permission_are_refused_before_validation_or_existence_disclosure()
    {
        await ResetAsync();
        await AddRoleAsync("no_positions", Permissions.CandidatesRead, Permissions.PresetsManage, Permissions.DocumentsDownload);
        await AddUserAsync("manager", "manager@example.test", "rrhh_user");
        await AddUserAsync("outsider", "outsider@example.test", "no_positions");
        await using var factory = CreateFactory();
        using var manager = await ClientAsync(factory, "manager");
        var existing = await CreateAsync(manager, "Existente");
        using var outsider = await ClientAsync(factory, "outsider");

        foreach (var (valid, invalid) in RoutePairs(existing))
        {
            using var validResponse = await outsider.SendAsync(valid());
            using var invalidResponse = await outsider.SendAsync(invalid());
            Assert.Equal(HttpStatusCode.Forbidden, validResponse.StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, invalidResponse.StatusCode);
            Assert.Equal(await validResponse.Content.ReadAsStringAsync(), await invalidResponse.Content.ReadAsStringAsync());
        }
    }

    [Fact]
    public async Task Each_position_and_composed_permission_holds_independently()
    {
        await ResetAsync();
        await AddRoleAsync("positions_reader", Permissions.PositionsRead);
        await AddRoleAsync("positions_writer", Permissions.PositionsManage);
        await AddRoleAsync("positions_both", Permissions.PositionsRead, Permissions.PositionsManage);
        await AddUserAsync("manager", "manager@example.test", "rrhh_user");
        await AddUserAsync("reader", "reader@example.test", "positions_reader");
        await AddUserAsync("writer", "writer@example.test", "positions_writer");
        await AddUserAsync("both", "both@example.test", "positions_both");
        await using var factory = CreateFactory();
        using var manager = await ClientAsync(factory, "manager");
        var existing = await CreateAsync(manager, "Existente");

        // Read-only: reads succeed; every write, including close, is refused and changes nothing.
        using var reader = await ClientAsync(factory, "reader");
        Assert.Equal(HttpStatusCode.OK, (await reader.GetAsync($"/api/positions/{existing.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await reader.PostAsJsonAsync("/api/positions", Body("Nueva"))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await reader.PutAsJsonAsync($"/api/positions/{existing.Id}", UpdateBody(existing, status: "closed"))).StatusCode);

        // Manage-only: manage does not imply read.
        using var writer = await ClientAsync(factory, "writer");
        Assert.Equal(HttpStatusCode.Forbidden, (await writer.GetAsync("/api/positions")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await writer.GetAsync($"/api/positions/{existing.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await writer.PostAsJsonAsync("/api/positions", Body("Del escritor"))).StatusCode);

        // Position permissions without candidate, preset or document permissions reach none of those.
        using var both = await ClientAsync(factory, "both");
        using var search = await both.PostAsJsonAsync("/api/candidates/search", new { page = 1, pageSize = 25, filters = existing.Requirements });
        Assert.Equal(HttpStatusCode.Forbidden, search.StatusCode);
        Assert.DoesNotContain("totalCount", await search.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(HttpStatusCode.Forbidden, (await both.PostAsJsonAsync("/api/search-presets", new { name = "Copia", filters = existing.Requirements })).StatusCode);
        // The download route answers a missing capability exactly like a missing document.
        Assert.Equal(HttpStatusCode.NotFound, (await both.GetAsync($"/api/candidates/{Guid.CreateVersion7()}/documents/{Guid.CreateVersion7()}/content")).StatusCode);

        var stored = (await manager.GetFromJsonAsync<PositionResponse>($"/api/positions/{existing.Id}"))!;
        Assert.Equal("open", stored.Status);
        Assert.Equal(existing.Version, stored.Version);
    }

    private static IEnumerable<(Func<HttpRequestMessage> Valid, Func<HttpRequestMessage> Invalid)> RoutePairs(PositionResponse existing)
    {
        var missing = Guid.CreateVersion7();
        yield return (() => new(HttpMethod.Get, "/api/positions"), () => new(HttpMethod.Get, "/api/positions?status=nope&page=0&pageSize=999"));
        yield return (() => new(HttpMethod.Get, $"/api/positions/{existing.Id}"), () => new(HttpMethod.Get, $"/api/positions/{missing}"));
        yield return (
            () => new(HttpMethod.Post, "/api/positions") { Content = JsonContent.Create(Body("Válida")) },
            () => new(HttpMethod.Post, "/api/positions") { Content = JsonContent.Create(new { title = "", requirements = new { hasCv = "maybe" } }) });
        yield return (
            () => new(HttpMethod.Put, $"/api/positions/{existing.Id}") { Content = JsonContent.Create(UpdateBody(existing)) },
            () => new(HttpMethod.Put, $"/api/positions/{missing}") { Content = JsonContent.Create(new { title = "", status = "nope", version = 0 }) });
    }

    private static object Body(string title, string description = "", string location = "", object? requirements = null) =>
        new { title, description, location, requirements = requirements ?? new { } };

    private static object UpdateBody(PositionResponse position, string? title = null, string? status = null) => new
    {
        title = title ?? position.Title,
        description = position.Description,
        location = position.Location,
        status = status ?? position.Status,
        requirements = position.Requirements,
        version = position.Version,
    };

    private static async Task<PositionResponse> CreateAsync(HttpClient client, string title, string description = "", string location = "", object? requirements = null)
    {
        using var response = await client.PostAsJsonAsync("/api/positions", Body(title, description, location, requirements));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<PositionResponse>())!;
    }

    private static async Task<PositionPageResponse> ListAsync(HttpClient client, string query)
    {
        using var response = await client.GetAsync($"/api/positions{query}");
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<PositionPageResponse>())!;
    }

    private static async Task<string?> CodeAsync(HttpResponseMessage response)
    {
        using var json = System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.TryGetProperty("code", out var code) ? code.GetString() : null;
    }

    private static async Task<HttpClient> ClientAsync(WebApplicationFactory<Program> factory, string subject)
    {
        var client = factory.CreateClient();
        await AuthenticateAsync(client, subject, $"{subject}@example.test");
        return client;
    }

    private async Task AddRoleAsync(string name, params string[] permissions)
    {
        await using var db = NewDbContext();
        db.Roles.Add(new Role(Guid.CreateVersion7(), name, name, false, permissions, DateTimeOffset.UtcNow));
        await db.SaveChangesAsync();
    }

    private static async Task AuthenticateAsync(HttpClient client, string subject, string email)
    {
        using var response = await client.PostAsJsonAsync("/api/dev/token", new { subject, displayName = "Integration Caller", email }); response.EnsureSuccessStatusCode();
        var token = (await response.Content.ReadFromJsonAsync<DevelopmentTokenResponse>())!.AccessToken;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }
    private async Task ResetAsync() { await using var db = NewDbContext(); await db.Database.EnsureDeletedAsync(); await DatabaseInitializer.MigrateAsync(db, CancellationToken.None); await DatabaseInitializer.SeedCatalogsAsync(db, CancellationToken.None); }
    private async Task AddUserAsync(string subject, string email, string roleName) { await using var db = NewDbContext(); db.Users.Add(new User(Guid.CreateVersion7(), subject, "Integration User", email, roleName, DateTimeOffset.UtcNow)); await db.SaveChangesAsync(); }
    private ApplicationDbContext NewDbContext() => new(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(database.ConnectionString).Options);
    private WebApplicationFactory<Program> CreateFactory()
    {
        Environment.SetEnvironmentVariable("ConnectionStrings__ApplicationDatabase", database.ConnectionString); Environment.SetEnvironmentVariable("DevelopmentActor__Enabled", "false"); Environment.SetEnvironmentVariable("OperationWorker__Enabled", "false"); Environment.SetEnvironmentVariable("Authentication__SubjectClaim", "oid"); Environment.SetEnvironmentVariable("Authentication__DevelopmentIssuer__Enabled", "true"); Environment.SetEnvironmentVariable("Authentication__DevelopmentIssuer__SigningKey", SigningKey); Environment.SetEnvironmentVariable("Authentication__DevelopmentIssuer__Issuer", Issuer); Environment.SetEnvironmentVariable("Authentication__DevelopmentIssuer__Audience", Audience);
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder => { builder.UseEnvironment("Testing"); builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:ApplicationDatabase"] = database.ConnectionString, ["DevelopmentActor:Enabled"] = "false", ["Authentication:SubjectClaim"] = "oid", ["Authentication:DevelopmentIssuer:Enabled"] = "true", ["Authentication:DevelopmentIssuer:SigningKey"] = SigningKey, ["Authentication:DevelopmentIssuer:Issuer"] = Issuer, ["Authentication:DevelopmentIssuer:Audience"] = Audience, ["ProxyTrust:KnownNetworks:0"] = "127.0.0.0/8" })); });
    }
    private sealed record DevelopmentTokenResponse(string AccessToken);
}
