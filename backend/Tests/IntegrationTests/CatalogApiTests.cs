using System.Net;
using System.Net.Http.Json;
using KeplerTalento.Application.Abstractions.Identity;
using KeplerTalento.Application.Features.Catalogs;
using KeplerTalento.Domain.Catalogs;
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
/// Exercises the catalog slices through the real HTTP → Application → PostgreSQL path
/// against a disposable PostgreSQL instance.
/// </summary>
public sealed class CatalogApiTests(PostgreSqlFixture database) : IClassFixture<PostgreSqlFixture>
{
    private const string Family = CatalogFamilies.Language;

    [Fact]
    public async Task Catalog_lifecycle_runs_through_http_application_and_postgresql()
    {
        await ResetAsync();
        await using var factory = CreateFactory(TestActor.Manager);
        using var client = factory.CreateClient();

        // list the seeded family
        var seeded = await client.GetFromJsonAsync<CatalogItemResponse[]>($"/api/catalogs/{Family}");
        Assert.Equal(["Inglés", "Francés", "Alemán", "Italiano", "Portugués"], seeded!.Select(item => item.NameEs));

        // create
        var createResponse = await client.PostAsJsonAsync(
            $"/api/catalogs/{Family}",
            new { nameEs = "Neerlandés", code = (string?)null, nameEn = "Dutch" });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = (await createResponse.Content.ReadFromJsonAsync<CatalogItemResponse>())!;
        Assert.Equal("NEERLANDES", created.Code);
        Assert.Equal(6, created.SortOrder);
        Assert.True(created.IsActive);

        // rename
        var renameResponse = await client.PutAsJsonAsync(
            $"/api/catalogs/{Family}/{created.Id}",
            new { nameEs = "Neerlandés (Países Bajos)", code = (string?)null, nameEn = "Dutch", version = created.Version });
        Assert.Equal(HttpStatusCode.OK, renameResponse.StatusCode);
        var renamed = (await renameResponse.Content.ReadFromJsonAsync<CatalogItemResponse>())!;
        Assert.Equal("Neerlandés (Países Bajos)", renamed.NameEs);
        Assert.Equal(6, renamed.SortOrder);

        // reorder: move the new value to the front
        var current = (await client.GetFromJsonAsync<CatalogItemResponse[]>($"/api/catalogs/{Family}?includeInactive=true"))!;
        var reordered = current.OrderByDescending(item => item.Id == created.Id).ThenBy(item => item.SortOrder).ToArray();
        var reorderResponse = await client.PutAsJsonAsync(
            $"/api/catalogs/{Family}/order",
            new { orderedIds = reordered.Select(item => item.Id).ToArray() });
        Assert.Equal(HttpStatusCode.OK, reorderResponse.StatusCode);
        var afterReorder = (await reorderResponse.Content.ReadFromJsonAsync<CatalogItemResponse[]>())!;
        Assert.Equal(created.Id, afterReorder[0].Id);
        Assert.Equal([1, 2, 3, 4, 5, 6], afterReorder.Select(item => item.SortOrder));

        // deactivate
        var stored = await ReadAsync(created.Id);
        var deactivateResponse = await client.PutAsJsonAsync(
            $"/api/catalogs/{Family}/{created.Id}/active",
            new { isActive = false, version = stored.Version });
        Assert.Equal(HttpStatusCode.OK, deactivateResponse.StatusCode);
        var active = (await client.GetFromJsonAsync<CatalogItemResponse[]>($"/api/catalogs/{Family}"))!;
        Assert.DoesNotContain(active, item => item.Id == created.Id);

        // the value is still stored, not deleted
        var afterDeactivation = await ReadAsync(created.Id);
        Assert.False(afterDeactivation.IsActive);

        // reactivate
        var reactivateResponse = await client.PutAsJsonAsync(
            $"/api/catalogs/{Family}/{created.Id}/active",
            new { isActive = true, version = afterDeactivation.Version });
        Assert.Equal(HttpStatusCode.OK, reactivateResponse.StatusCode);
        var reactivated = (await client.GetFromJsonAsync<CatalogItemResponse[]>($"/api/catalogs/{Family}"))!;
        Assert.Equal(created.Id, reactivated[0].Id);

        // every change is audited, and the reads are not
        await using var dbContext = NewDbContext();
        var auditedTypes = await dbContext.AuditEvents
            .AsNoTracking()
            .Where(audit => audit.EventType.StartsWith("catalog."))
            .Select(audit => audit.EventType)
            .ToListAsync();
        Assert.Contains(CatalogAuditEvents.Created, auditedTypes);
        Assert.Contains(CatalogAuditEvents.Updated, auditedTypes);
        Assert.Contains(CatalogAuditEvents.Reordered, auditedTypes);
        Assert.Equal(2, auditedTypes.Count(type => type == CatalogAuditEvents.ActivationChanged));
    }

    [Fact]
    public async Task Duplicate_name_is_rejected_with_the_stable_code_and_Spanish_message()
    {
        await ResetAsync();
        await using var factory = CreateFactory(TestActor.Manager);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            $"/api/catalogs/{Family}",
            new { nameEs = "  ingles  ", code = (string?)null, nameEn = (string?)null });
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(CatalogErrors.NameDuplicate, body, StringComparison.Ordinal);
        Assert.Contains("Ya existe un valor con ese nombre.", body, StringComparison.Ordinal);
        Assert.Equal(5, await CountAsync(Family));
    }

    [Fact]
    public async Task Concurrent_creation_of_the_same_name_yields_one_success_and_one_duplicate()
    {
        await ResetAsync();
        await using var factory = CreateFactory(TestActor.Manager);

        var payload = new { nameEs = "Catalán", code = (string?)null, nameEn = (string?)null };
        var responses = await Task.WhenAll(Enumerable.Range(0, 2).Select(async _ =>
        {
            using var client = factory.CreateClient();
            var response = await client.PostAsJsonAsync($"/api/catalogs/{Family}", payload);
            return (response.StatusCode, Body: await response.Content.ReadAsStringAsync());
        }));

        Assert.Equal(1, responses.Count(result => result.StatusCode == HttpStatusCode.Created));
        var rejected = Assert.Single(responses, result => result.StatusCode != HttpStatusCode.Created);
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        Assert.Contains(CatalogErrors.NameDuplicate, rejected.Body, StringComparison.Ordinal);

        await using var dbContext = NewDbContext();
        Assert.Single(await dbContext.CatalogItems
            .AsNoTracking()
            .Where(item => item.Family == Family && item.NameNormalized == "catalan")
            .ToListAsync());
    }

    [Fact]
    public async Task A_stale_version_is_rejected_as_a_conflict()
    {
        await ResetAsync();
        await using var factory = CreateFactory(TestActor.Manager);
        using var client = factory.CreateClient();
        var items = (await client.GetFromJsonAsync<CatalogItemResponse[]>($"/api/catalogs/{Family}"))!;
        var target = items[0];

        var first = await client.PutAsJsonAsync(
            $"/api/catalogs/{Family}/{target.Id}",
            new { nameEs = "Inglés británico", code = (string?)null, nameEn = (string?)null, version = target.Version });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var second = await client.PutAsJsonAsync(
            $"/api/catalogs/{Family}/{target.Id}",
            new { nameEs = "Inglés americano", code = (string?)null, nameEn = (string?)null, version = target.Version });
        var body = await second.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Contains(CatalogErrors.ConcurrencyConflict, body, StringComparison.Ordinal);
        Assert.Equal("Inglés británico", (await ReadAsync(target.Id)).NameEs);
    }

    [Fact]
    public async Task Unauthenticated_caller_is_denied_and_stores_nothing()
    {
        await ResetAsync();
        await using var factory = CreateFactory(TestActor.Unauthenticated);
        using var client = factory.CreateClient();

        var list = await client.GetAsync($"/api/catalogs/{Family}");
        var create = await client.PostAsJsonAsync(
            $"/api/catalogs/{Family}",
            new { nameEs = "Sueco", code = (string?)null, nameEn = (string?)null });

        Assert.Equal(HttpStatusCode.Forbidden, list.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, create.StatusCode);
        Assert.DoesNotContain("Inglés", await list.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        Assert.Equal(5, await CountAsync(Family));
    }

    [Fact]
    public async Task Reader_can_list_but_cannot_change_anything()
    {
        await ResetAsync();
        await using var factory = CreateFactory(TestActor.Reader);
        using var client = factory.CreateClient();
        var items = (await client.GetFromJsonAsync<CatalogItemResponse[]>($"/api/catalogs/{Family}"))!;
        var target = items[0];

        var create = await client.PostAsJsonAsync(
            $"/api/catalogs/{Family}",
            new { nameEs = "Sueco", code = (string?)null, nameEn = (string?)null });
        var update = await client.PutAsJsonAsync(
            $"/api/catalogs/{Family}/{target.Id}",
            new { nameEs = "Otro", code = (string?)null, nameEn = (string?)null, version = target.Version });
        var reorder = await client.PutAsJsonAsync(
            $"/api/catalogs/{Family}/order",
            new { orderedIds = items.Select(item => item.Id).Reverse().ToArray() });
        var activate = await client.PutAsJsonAsync(
            $"/api/catalogs/{Family}/{target.Id}/active",
            new { isActive = false, version = target.Version });

        Assert.All(
            new[] { create, update, reorder, activate },
            response => Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode));
        var stored = await ReadAsync(target.Id);
        Assert.Equal("Inglés", stored.NameEs);
        Assert.True(stored.IsActive);
        Assert.Equal(5, await CountAsync(Family));
    }

    [Fact]
    public async Task No_delete_verb_is_exposed_for_catalog_values()
    {
        await ResetAsync();
        await using var factory = CreateFactory(TestActor.Manager);
        using var client = factory.CreateClient();
        var items = (await client.GetFromJsonAsync<CatalogItemResponse[]>($"/api/catalogs/{Family}"))!;

        var response = await client.DeleteAsync($"/api/catalogs/{Family}/{items[0].Id}");

        Assert.Contains(
            response.StatusCode,
            new[] { HttpStatusCode.NotFound, HttpStatusCode.MethodNotAllowed });
        Assert.Equal(5, await CountAsync(Family));
    }

    [Fact]
    public async Task Runtime_role_cannot_delete_catalog_values()
    {
        await ResetAsync();

        await using var connection = new NpgsqlConnection(database.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            """
            SELECT has_table_privilege('ktl_runtime', '"CAT_CatalogItems"', 'SELECT'),
                   has_table_privilege('ktl_runtime', '"CAT_CatalogItems"', 'INSERT'),
                   has_table_privilege('ktl_runtime', '"CAT_CatalogItems"', 'UPDATE'),
                   has_table_privilege('ktl_runtime', '"CAT_CatalogItems"', 'DELETE'),
                   has_table_privilege('ktl_runtime', '"CAT_CatalogItems"', 'TRUNCATE')
            """,
            connection);
        await using var reader = await command.ExecuteReaderAsync();

        Assert.True(await reader.ReadAsync());
        Assert.True(reader.GetBoolean(0));
        Assert.True(reader.GetBoolean(1));
        Assert.True(reader.GetBoolean(2));
        Assert.False(reader.GetBoolean(3));
        Assert.False(reader.GetBoolean(4));
    }

    [Fact]
    public async Task Seed_is_idempotent_and_preserves_administrator_edits()
    {
        await ResetAsync();
        await using var dbContext = NewDbContext();

        var edited = await dbContext.CatalogItems
            .SingleAsync(item => item.Family == Family && item.NameNormalized == "ingles");
        edited.Rename("Inglés técnico", null, DateTimeOffset.UtcNow);
        var removed = await dbContext.CatalogItems
            .SingleAsync(item => item.Family == Family && item.NameNormalized == "italiano");
        removed.SetActive(false, DateTimeOffset.UtcNow);
        await dbContext.SaveChangesAsync();

        await DatabaseInitializer.SeedCatalogsAsync(dbContext, CancellationToken.None);
        await DatabaseInitializer.SeedCatalogsAsync(dbContext, CancellationToken.None);

        var family = await dbContext.CatalogItems
            .AsNoTracking()
            .Where(item => item.Family == Family)
            .OrderBy(item => item.SortOrder)
            .ToListAsync();
        Assert.Equal(5, family.Count);
        Assert.Equal("Inglés técnico", family[0].NameEs);
        Assert.False(family.Single(item => item.NameNormalized == "italiano").IsActive);

        // every family is seeded with its accented Spanish defaults
        var sectors = await dbContext.CatalogItems
            .AsNoTracking()
            .Where(item => item.Family == CatalogFamilies.Sector)
            .OrderBy(item => item.SortOrder)
            .Select(item => item.NameEs)
            .ToListAsync();
        Assert.Equal(["Servicios", "Industria", "Tecnología", "Comercio", "Sanidad", "Educación"], sectors);
        Assert.Equal(
            CatalogFamilies.All.Count,
            await dbContext.CatalogItems.AsNoTracking().Select(item => item.Family).Distinct().CountAsync());
    }

    private async Task ResetAsync()
    {
        await using var dbContext = NewDbContext();
        await DatabaseInitializer.MigrateAsync(dbContext, CancellationToken.None);
        await dbContext.CatalogItems.ExecuteDeleteAsync();
        await dbContext.AuditEvents.Where(audit => audit.EventType.StartsWith("catalog.")).ExecuteDeleteAsync();
        await DatabaseInitializer.SeedCatalogsAsync(dbContext, CancellationToken.None);
    }

    private async Task<CatalogItem> ReadAsync(Guid id)
    {
        await using var dbContext = NewDbContext();
        return await dbContext.CatalogItems.AsNoTracking().SingleAsync(item => item.Id == id);
    }

    private async Task<int> CountAsync(string family)
    {
        await using var dbContext = NewDbContext();
        return await dbContext.CatalogItems.AsNoTracking().CountAsync(item => item.Family == family);
    }

    private ApplicationDbContext NewDbContext() => new(
        new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(database.ConnectionString).Options);

    private WebApplicationFactory<Program> CreateFactory(ICurrentActor actor)
    {
        // The host reads the connection string while registering infrastructure, before the
        // factory's configuration overrides are applied, so the disposable database has to be
        // supplied through the environment.
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
        public static TestActor Manager => new(true, Permissions.CatalogsRead, Permissions.CatalogsManage);
        public static TestActor Reader => new(true, Permissions.CatalogsRead);
        public static TestActor Unauthenticated => new(false);

        public string? ExternalKey => authenticated ? "integration-actor" : null;
        public bool IsAuthenticated => authenticated;
        public bool HasPermission(string permission) =>
            authenticated && permissions.Contains(permission, StringComparer.Ordinal);
    }
}
