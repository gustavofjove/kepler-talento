using System.Net;
using System.Net.Http.Json;
using KeplerTalento.Application.Abstractions.Correlation;
using KeplerTalento.Application.Abstractions.Identity;
using KeplerTalento.Application.Abstractions.Persistence;
using KeplerTalento.Application.Features.Catalogs;
using KeplerTalento.Domain.Catalogs;
using KeplerTalento.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
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
[Collection(WebHostCollection.Name)]
public sealed class CatalogApiTests(PostgreSqlFixture database) : IClassFixture<PostgreSqlFixture>
{
    private const string Family = CatalogFamilies.Language;

    /// <summary>
    /// Taken from the seed rather than written as a literal: these tests are about what the
    /// endpoints do to a seeded family, not about how many values the vocabulary happens to
    /// hold, and widening a catalog should not fail them.
    /// </summary>
    private static readonly IReadOnlyList<string> SeededNames =
        [.. CatalogSeedData.Families[Family].Select(value => value.NameEs)];

    private static int SeededCount => SeededNames.Count;

    [Fact]
    public async Task Catalog_lifecycle_runs_through_http_application_and_postgresql()
    {
        await ResetAsync();
        await using var factory = CreateFactory(TestActor.Manager);
        using var client = factory.CreateClient();

        // list the seeded family
        var seeded = await client.GetFromJsonAsync<CatalogItemResponse[]>($"/api/catalogs/{Family}");
        Assert.Equal(SeededNames, seeded!.Select(item => item.NameEs));

        // create
        var createResponse = await client.PostAsJsonAsync(
            $"/api/catalogs/{Family}",
            new { nameEs = "Neerlandés", code = (string?)null, nameEn = "Dutch" });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = (await createResponse.Content.ReadFromJsonAsync<CatalogItemResponse>())!;
        Assert.Equal("NEERLANDES", created.Code);
        Assert.Equal(SeededCount + 1, created.SortOrder);
        Assert.True(created.IsActive);

        // rename
        var renameResponse = await client.PutAsJsonAsync(
            $"/api/catalogs/{Family}/{created.Id}",
            new { nameEs = "Neerlandés (Países Bajos)", code = (string?)null, nameEn = "Dutch", version = created.Version });
        Assert.Equal(HttpStatusCode.OK, renameResponse.StatusCode);
        var renamed = (await renameResponse.Content.ReadFromJsonAsync<CatalogItemResponse>())!;
        Assert.Equal("Neerlandés (Países Bajos)", renamed.NameEs);
        Assert.Equal(SeededCount + 1, renamed.SortOrder);

        // reorder: move the new value to the front
        var current = (await client.GetFromJsonAsync<CatalogItemResponse[]>($"/api/catalogs/{Family}?includeInactive=true"))!;
        var reordered = current.OrderByDescending(item => item.Id == created.Id).ThenBy(item => item.SortOrder).ToArray();
        var reorderResponse = await client.PutAsJsonAsync(
            $"/api/catalogs/{Family}/order",
            new { orderedIds = reordered.Select(item => item.Id).ToArray() });
        Assert.Equal(HttpStatusCode.OK, reorderResponse.StatusCode);
        var afterReorder = (await reorderResponse.Content.ReadFromJsonAsync<CatalogItemResponse[]>())!;
        Assert.Equal(created.Id, afterReorder[0].Id);
        Assert.Equal(Enumerable.Range(1, SeededCount + 1), afterReorder.Select(item => item.SortOrder));

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
        var audited = await dbContext.AuditEvents
            .AsNoTracking()
            .Where(audit => audit.EventType.StartsWith("catalog."))
            .ToListAsync();
        var auditedTypes = audited.Select(audit => audit.EventType).ToList();
        // Every catalog write names the acting user, and nothing else identifying them.
        Assert.All(audited, audit => Assert.Equal(TestActor.StoredUserId, audit.ActorUserId));
        Assert.All(audited, audit => Assert.Null(audit.OutcomeCode));
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
        Assert.Equal(SeededCount, await CountAsync(Family));
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
    public async Task Stale_disjoint_reorders_cannot_commit_a_mixed_family_order()
    {
        await ResetAsync();
        await using var firstContext = NewDbContext();
        await using var secondContext = NewDbContext();
        var first = new CatalogRepository(firstContext, new TestCorrelationContext(), TestActor.Manager);
        var second = new CatalogRepository(secondContext, new TestCorrelationContext(), TestActor.Manager);

        // Both requests read the same order before either saves. Their swaps touch disjoint
        // rows, so per-row concurrency on changed items alone would let both succeed.
        var firstItems = await first.ListAsync(Family, includeInactive: true, CancellationToken.None);
        var secondItems = await second.ListAsync(Family, includeInactive: true, CancellationToken.None);
        var firstOrder = firstItems.Select(item => item.Id).ToArray();
        (firstOrder[0], firstOrder[1]) = (firstOrder[1], firstOrder[0]);
        firstItems[0].MoveTo(2, DateTimeOffset.UtcNow);
        firstItems[1].MoveTo(1, DateTimeOffset.UtcNow);
        secondItems[2].MoveTo(4, DateTimeOffset.UtcNow);
        secondItems[3].MoveTo(3, DateTimeOffset.UtcNow);

        Assert.Equal(CatalogSaveOutcome.Saved,
            await first.SaveAsync(CatalogAuditEvents.Reordered, Family, CancellationToken.None));
        Assert.Equal(CatalogSaveOutcome.ConcurrencyConflict,
            await second.SaveAsync(CatalogAuditEvents.Reordered, Family, CancellationToken.None));

        await using var check = NewDbContext();
        var stored = await check.CatalogItems.AsNoTracking().Where(item => item.Family == Family)
            .OrderBy(item => item.SortOrder).Select(item => item.Id).ToArrayAsync();
        Assert.Equal(firstOrder, stored);
        Assert.Equal(1, await check.AuditEvents.CountAsync(audit =>
            audit.EventType == CatalogAuditEvents.Reordered && audit.SubjectId == Family));
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
        Assert.Equal(SeededCount, await CountAsync(Family));
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
        Assert.Equal(SeededCount, await CountAsync(Family));
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
        Assert.Equal(SeededCount, await CountAsync(Family));
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
    public async Task Colour_round_trips_through_create_update_and_list()
    {
        await ResetAsync();
        await using var factory = CreateFactory(TestActor.Manager);
        using var client = factory.CreateClient();

        // every seeded value starts in the default colour
        var seeded = (await client.GetFromJsonAsync<CatalogItemResponse[]>($"/api/catalogs/{Family}"))!;
        Assert.All(seeded, item => Assert.Equal(CatalogColors.Default, item.Color));

        // create with a colour, and without one
        var withColour = await client.PostAsJsonAsync(
            $"/api/catalogs/{CatalogFamilies.Tag}",
            new { nameEs = "Urgente", code = (string?)null, nameEn = (string?)null, color = "pink" });
        Assert.Equal(HttpStatusCode.Created, withColour.StatusCode);
        var tag = (await withColour.Content.ReadFromJsonAsync<CatalogItemResponse>())!;
        Assert.Equal("pink", tag.Color);
        var withoutColour = await client.PostAsJsonAsync(
            $"/api/catalogs/{Family}",
            new { nameEs = "Sueco", code = (string?)null, nameEn = (string?)null });
        Assert.Equal(CatalogColors.Default, (await withoutColour.Content.ReadFromJsonAsync<CatalogItemResponse>())!.Color);

        // recolour under the version check
        var recolour = await client.PutAsJsonAsync(
            $"/api/catalogs/{CatalogFamilies.Tag}/{tag.Id}",
            new { nameEs = "Urgente", code = (string?)null, nameEn = (string?)null, version = tag.Version, color = "blue" });
        Assert.Equal(HttpStatusCode.OK, recolour.StatusCode);
        var recoloured = (await recolour.Content.ReadFromJsonAsync<CatalogItemResponse>())!;
        Assert.Equal("blue", recoloured.Color);

        // a rename without a colour keeps it
        var rename = await client.PutAsJsonAsync(
            $"/api/catalogs/{CatalogFamilies.Tag}/{tag.Id}",
            new { nameEs = "Muy urgente", code = (string?)null, nameEn = (string?)null, version = recoloured.Version });
        Assert.Equal(HttpStatusCode.OK, rename.StatusCode);
        Assert.Equal("blue", (await ReadAsync(tag.Id)).Color);

        // deactivation keeps it too, and the list reports it
        var stored = await ReadAsync(tag.Id);
        await client.PutAsJsonAsync(
            $"/api/catalogs/{CatalogFamilies.Tag}/{tag.Id}/active",
            new { isActive = false, version = stored.Version });
        var listed = (await client.GetFromJsonAsync<CatalogItemResponse[]>(
            $"/api/catalogs/{CatalogFamilies.Tag}?includeInactive=true"))!;
        Assert.Equal("blue", listed.Single(item => item.Id == tag.Id).Color);

        // the colour change is audited as an update by the acting user
        await using var dbContext = NewDbContext();
        Assert.Equal(2, await dbContext.AuditEvents.CountAsync(audit =>
            audit.EventType == CatalogAuditEvents.Updated
            && audit.SubjectId == tag.Id.ToString("N")
            && audit.ActorUserId == TestActor.StoredUserId));
    }

    [Theory]
    [InlineData(CatalogFamilies.Language, "magenta", CatalogErrors.ColorInvalid, "El color no es válido.")]
    [InlineData(CatalogFamilies.Language, "Blue", CatalogErrors.ColorInvalid, "El color no es válido.")]
    [InlineData(CatalogFamilies.LanguageLevel, "blue", CatalogErrors.ColorNotSupported, "Esta familia de catálogo no admite color.")]
    [InlineData(CatalogFamilies.Sector, "green", CatalogErrors.ColorNotSupported, "Esta familia de catálogo no admite color.")]
    public async Task Invalid_colour_is_rejected_and_stores_nothing(
        string family,
        string color,
        string code,
        string message)
    {
        await ResetAsync();
        await using var factory = CreateFactory(TestActor.Manager);
        using var client = factory.CreateClient();
        var target = (await client.GetFromJsonAsync<CatalogItemResponse[]>($"/api/catalogs/{family}"))![0];
        var countBefore = await CountAsync(family);

        var create = await client.PostAsJsonAsync(
            $"/api/catalogs/{family}",
            new { nameEs = "Nuevo valor", code = (string?)null, nameEn = (string?)null, color });
        var update = await client.PutAsJsonAsync(
            $"/api/catalogs/{family}/{target.Id}",
            new { nameEs = target.NameEs, code = (string?)null, nameEn = (string?)null, version = target.Version, color });

        foreach (var response in new[] { create, update })
        {
            var body = await response.Content.ReadAsStringAsync();
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Contains(code, body, StringComparison.Ordinal);
            Assert.Contains(message, body, StringComparison.Ordinal);
        }
        Assert.Equal(countBefore, await CountAsync(family));
        Assert.Equal(CatalogColors.Default, (await ReadAsync(target.Id)).Color);
        await using var dbContext = NewDbContext();
        Assert.False(await dbContext.AuditEvents.AnyAsync(audit => audit.EventType.StartsWith("catalog.")));
    }

    [Fact]
    public async Task A_stale_version_rejects_a_colour_change()
    {
        await ResetAsync();
        await using var factory = CreateFactory(TestActor.Manager);
        using var client = factory.CreateClient();
        var target = (await client.GetFromJsonAsync<CatalogItemResponse[]>($"/api/catalogs/{Family}"))![0];
        await client.PutAsJsonAsync(
            $"/api/catalogs/{Family}/{target.Id}",
            new { nameEs = target.NameEs, code = (string?)null, nameEn = (string?)null, version = target.Version, color = "green" });

        var stale = await client.PutAsJsonAsync(
            $"/api/catalogs/{Family}/{target.Id}",
            new { nameEs = target.NameEs, code = (string?)null, nameEn = (string?)null, version = target.Version, color = "violet" });

        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Contains(CatalogErrors.ConcurrencyConflict, await stale.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        Assert.Equal("green", (await ReadAsync(target.Id)).Color);
    }

    [Fact]
    public async Task Colour_changes_fail_closed_before_validation()
    {
        await ResetAsync();
        CatalogItemResponse target;
        await using (var managerFactory = CreateFactory(TestActor.Manager))
        {
            using var managerClient = managerFactory.CreateClient();
            target = (await managerClient.GetFromJsonAsync<CatalogItemResponse[]>($"/api/catalogs/{Family}"))![0];
        }

        foreach (var actor in new[] { TestActor.Unauthenticated, TestActor.Reader })
        {
            await using var factory = CreateFactory(actor);
            using var client = factory.CreateClient();

            // An invalid colour still yields 403, not 400: authorization runs first, so a caller
            // without the capability cannot probe the palette through validation problems.
            foreach (var color in new[] { "magenta", "blue" })
            {
                var create = await client.PostAsJsonAsync(
                    $"/api/catalogs/{Family}",
                    new { nameEs = "Sueco", code = (string?)null, nameEn = (string?)null, color });
                var update = await client.PutAsJsonAsync(
                    $"/api/catalogs/{Family}/{target.Id}",
                    new { nameEs = target.NameEs, code = (string?)null, nameEn = (string?)null, version = target.Version, color });

                Assert.Equal(HttpStatusCode.Forbidden, create.StatusCode);
                Assert.Equal(HttpStatusCode.Forbidden, update.StatusCode);
                Assert.DoesNotContain(CatalogErrors.ColorInvalid, await create.Content.ReadAsStringAsync(), StringComparison.Ordinal);
            }
        }

        Assert.Equal(CatalogColors.Default, (await ReadAsync(target.Id)).Color);
        Assert.Equal(SeededCount, await CountAsync(Family));
        await using var dbContext = NewDbContext();
        Assert.False(await dbContext.AuditEvents.AnyAsync(audit => audit.EventType.StartsWith("catalog.")));
    }

    [Fact]
    public async Task Migration_backfills_existing_values_with_the_default_colour()
    {
        await ResetAsync();
        await using var dbContext = NewDbContext();
        var migrator = dbContext.GetService<IMigrator>();
        var applied = (await dbContext.Database.GetAppliedMigrationsAsync()).ToList();
        var colourMigration = applied.Single(id => id.EndsWith("_AddCatalogItemColor", StringComparison.Ordinal));
        var previous = applied[applied.IndexOf(colourMigration) - 1];

        try
        {
            // Roll back to the schema before KTL-41, write a value the old way, then migrate.
            await migrator.MigrateAsync(previous);
            var id = Guid.CreateVersion7();
            await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "CAT_CatalogItems"
                    ("Id", "Family", "Code", "NameEs", "NameNormalized", "SortOrder", "IsActive", "CreatedAtUtc", "UpdatedAtUtc")
                VALUES ({id}, 'tag', 'PRE_KTL41', 'Previo', 'previo', 999, TRUE, now(), now())
                """);
            await migrator.MigrateAsync();

            Assert.Equal(CatalogColors.Default, (await ReadAsync(id)).Color);
        }
        finally
        {
            await migrator.MigrateAsync();
        }
    }

    [Fact]
    public async Task Database_rejects_a_colour_outside_the_palette()
    {
        await ResetAsync();
        await using var connection = new NpgsqlConnection(database.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            """UPDATE "CAT_CatalogItems" SET "Color" = 'magenta' WHERE "Family" = 'language'""",
            connection);

        var exception = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());

        Assert.Equal(PostgresErrorCodes.CheckViolation, exception.SqlState);
        Assert.Equal("CK_CAT_CatalogItems_Color", exception.ConstraintName);
    }

    [Fact]
    public async Task Runtime_role_can_update_the_colour_but_still_cannot_delete()
    {
        await ResetAsync();
        await using var connection = new NpgsqlConnection(database.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            """
            SELECT has_column_privilege('ktl_runtime', '"CAT_CatalogItems"', 'Color', 'SELECT'),
                   has_column_privilege('ktl_runtime', '"CAT_CatalogItems"', 'Color', 'UPDATE'),
                   has_table_privilege('ktl_runtime', '"CAT_CatalogItems"', 'DELETE')
            """,
            connection);
        await using var reader = await command.ExecuteReaderAsync();

        Assert.True(await reader.ReadAsync());
        Assert.True(reader.GetBoolean(0));
        Assert.True(reader.GetBoolean(1));
        Assert.False(reader.GetBoolean(2));
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
        Assert.Equal(SeededCount, family.Count);
        Assert.Equal("Inglés técnico", family[0].NameEs);
        Assert.False(family.Single(item => item.NameNormalized == "italiano").IsActive);

        // every family is seeded with its accented Spanish defaults
        var sectors = await dbContext.CatalogItems
            .AsNoTracking()
            .Where(item => item.Family == CatalogFamilies.Sector)
            .OrderBy(item => item.SortOrder)
            .Select(item => item.NameEs)
            .ToListAsync();
        Assert.Equal(
            CatalogSeedData.Families[CatalogFamilies.Sector].Select(value => value.NameEs),
            sectors);
        // Accents survive the round trip, which is the point of asserting on names at all.
        Assert.Contains("Tecnología", sectors, StringComparer.Ordinal);
        Assert.Equal(
            CatalogFamilies.All.Count,
            await dbContext.CatalogItems.AsNoTracking().Select(item => item.Family).Distinct().CountAsync());
    }

    [Fact]
    public async Task Seed_adds_tags_to_an_existing_nine_family_database_without_altering_it()
    {
        await ResetAsync();
        await using var dbContext = NewDbContext();

        await dbContext.CatalogItems
            .Where(item => item.Family == CatalogFamilies.Tag)
            .ExecuteDeleteAsync();
        var before = await dbContext.CatalogItems
            .AsNoTracking()
            .Where(item => item.Family != CatalogFamilies.Tag)
            .OrderBy(item => item.Family)
            .ThenBy(item => item.SortOrder)
            .Select(item => new { item.Id, item.Family, item.NameEs, item.SortOrder, item.IsActive })
            .ToListAsync();

        await DatabaseInitializer.SeedCatalogsAsync(dbContext, CancellationToken.None);

        var after = await dbContext.CatalogItems
            .AsNoTracking()
            .Where(item => item.Family != CatalogFamilies.Tag)
            .OrderBy(item => item.Family)
            .ThenBy(item => item.SortOrder)
            .Select(item => new { item.Id, item.Family, item.NameEs, item.SortOrder, item.IsActive })
            .ToListAsync();
        var tags = await dbContext.CatalogItems
            .AsNoTracking()
            .Where(item => item.Family == CatalogFamilies.Tag)
            .OrderBy(item => item.SortOrder)
            .Select(item => item.NameEs)
            .ToListAsync();

        Assert.Equal(before, after);
        Assert.Equal(
            CatalogSeedData.Families[CatalogFamilies.Tag].Select(value => value.NameEs),
            tags);
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
        new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(database.ConnectionString).UseTestFieldEncryption().Options);

    private WebApplicationFactory<Program> CreateFactory(ICurrentActor actor)
    {
        // The host reads the connection string while registering infrastructure, before the
        // factory's configuration overrides are applied, so the disposable database has to be
        // supplied through the environment.
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

    private sealed class TestActor(bool authenticated, params string[] permissions) : ICurrentActor
    {
        public static TestActor Manager => new(true, Permissions.CatalogsRead, Permissions.CatalogsManage);
        public static TestActor Reader => new(true, Permissions.CatalogsRead);
        public static TestActor Unauthenticated => new(false);

        public string? ExternalKey => authenticated ? "integration-actor" : null;

        /// <summary>The internal user id audit events name. No row is needed: the audit table has no foreign key.</summary>
        public static readonly Guid StoredUserId = Guid.Parse("01932f00-0000-7000-8000-00000000d001");

        public Guid? UserId => authenticated ? StoredUserId : null;

        public bool IsAuthenticated => authenticated;
        public bool HasPermission(string permission) =>
            authenticated && permissions.Contains(permission, StringComparer.Ordinal);
    }

    private sealed class TestCorrelationContext : ICorrelationContext
    {
        public string CorrelationId => "ktl-25-reorder-concurrency";
    }
}
