using System.Text.Json;
using KeplerTalento.Application.Features.Search;
using KeplerTalento.Domain.Positions;
using KeplerTalento.Domain.Search;
using KeplerTalento.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Xunit;

namespace KeplerTalento.Tests.IntegrationTests;

/// <summary>
/// Evidence for the stored-filter rewrite of <c>ReplaceCandidateStatusWithAvailability</c>
/// (KTL-36 design D6): version 1 presets and position requirements become version 2 without the
/// encrypted <c>text</c> member being read or rewritten, and the migration rolls back too.
/// </summary>
public sealed class FilterSchemaVersionTwoMigrationTests(PostgreSqlFixture database) : IClassFixture<PostgreSqlFixture>
{
    private const string BeforeAvailability = "20260929133601_EncryptPersonalData";

    [Fact]
    public async Task Version_one_presets_and_requirements_are_rewritten_with_their_ciphertext_untouched()
    {
        await using (var reset = NewContext())
        {
            await reset.Database.EnsureDeletedAsync();
            await DatabaseInitializer.MigrateAsync(reset, CancellationToken.None);
            await DatabaseInitializer.SeedCatalogsAsync(reset, CancellationToken.None);
        }

        // Written through the current model, so the text member is a genuine envelope.
        var filters = SearchFilterDocument.Serialize(SearchFilterNormalization.Normalize(
            new SearchFiltersInput("Lucía Fernández", null, [new SearchCriterionInput("Java", "")], "ANY", null, null, null, null, "yes"),
            "Filters"));
        var presetId = Guid.CreateVersion7();
        var positionId = Guid.CreateVersion7();
        await using (var write = NewContext())
        {
            write.SearchPresets.Add(new SearchPreset(presetId, "Perfil Java", filters, SearchFilterNormalization.FilterSchemaVersion, DateTimeOffset.UtcNow));
            write.Positions.Add(new Position(positionId, "Desarrollador Java", string.Empty, "Madrid", filters, SearchFilterNormalization.FilterSchemaVersion, DateTimeOffset.UtcNow));
            await write.SaveChangesAsync();
        }

        // Down: back to version 1, with every status selected.
        await using (var rollback = NewContext())
        {
            await rollback.GetService<IMigrator>().MigrateAsync(BeforeAvailability);
        }
        var (presetV1, positionV1) = await ReadStoredAsync(presetId, positionId);
        foreach (var stored in new[] { presetV1, positionV1 })
        {
            Assert.Equal(1, stored.Version);
            Assert.Equal(1, stored.Document.GetProperty("version").GetInt32());
            Assert.Equal(5, stored.Document.GetProperty("statusValues").GetArrayLength());
            Assert.False(stored.Document.TryGetProperty("availabilityValues", out _));
        }

        // A restricted status selection, as a version 1 preset saved before KTL-36 could hold.
        await using (var connection = new NpgsqlConnection(database.ConnectionString))
        {
            await connection.OpenAsync();
            await using var narrow = new NpgsqlCommand(
                """
                UPDATE "ADM_SearchPresets" SET "Filters" = jsonb_set("Filters", '{statusValues}', '["hired"]'::jsonb);
                UPDATE "OPS_Positions" SET "Requirements" = jsonb_set("Requirements", '{statusValues}', '["in_process"]'::jsonb);
                """,
                connection);
            await narrow.ExecuteNonQueryAsync();
        }
        var (narrowedPreset, narrowedPosition) = await ReadStoredAsync(presetId, positionId);

        // Up: version 2, status selection dropped, unrestricted availability family.
        await using (var migrate = NewContext())
        {
            await DatabaseInitializer.MigrateAsync(migrate, CancellationToken.None);
        }
        var (presetV2, positionV2) = await ReadStoredAsync(presetId, positionId);
        foreach (var (before, after) in new[] { (narrowedPreset, presetV2), (narrowedPosition, positionV2) })
        {
            Assert.Equal(2, after.Version);
            Assert.Equal(2, after.Document.GetProperty("version").GetInt32());
            Assert.False(after.Document.TryGetProperty("statusValues", out _));
            Assert.Equal(
                ["unknown", "available", "unavailable"],
                after.Document.GetProperty("availabilityValues").EnumerateArray().Select(value => value.GetString()));
            Assert.Equal(string.Empty, after.Document.GetProperty("availabilityCheckedFrom").GetString());
            // The ciphertext is byte-for-byte the same envelope: nothing was decrypted or re-encrypted.
            Assert.Equal(before.Document.GetProperty("text").GetString(), after.Document.GetProperty("text").GetString());
            Assert.DoesNotContain("Lucía", after.Raw, StringComparison.Ordinal);
            Assert.Equal(
                before.Document.GetProperty("skillCriteria").GetRawText(),
                after.Document.GetProperty("skillCriteria").GetRawText());
        }

        // Both still parse and apply under the strict version 2 reader.
        await using var read = NewContext();
        var preset = await read.SearchPresets.AsNoTracking().SingleAsync(value => value.Id == presetId);
        var position = await read.Positions.AsNoTracking().SingleAsync(value => value.Id == positionId);
        foreach (var json in new[] { preset.Filters, position.Requirements })
        {
            var parsed = SearchFilterDocument.Parse(json);
            Assert.Equal("Lucía Fernández", parsed.Text);
            Assert.True(parsed.AvailabilityIsUnrestricted);
            Assert.Null(parsed.AvailabilityCheckedFrom);
            var search = new CandidateSearchQuery(read);
            var page = await search.SearchAsync(
                parsed,
                new SearchOptions(1, SearchPaging.DefaultPageSize, SearchSort.Default, false),
                CancellationToken.None);
            Assert.Equal(0, page.TotalCount);
        }
    }

    private sealed record StoredDocument(int Version, JsonElement Document, string Raw);

    private async Task<(StoredDocument Preset, StoredDocument Position)> ReadStoredAsync(Guid presetId, Guid positionId)
    {
        await using var connection = new NpgsqlConnection(database.ConnectionString);
        await connection.OpenAsync();
        async Task<StoredDocument> ReadAsync(string sql, Guid id)
        {
            await using var command = new NpgsqlCommand(sql, connection);
            command.Parameters.AddWithValue("id", id);
            await using var reader = await command.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());
            var raw = reader.GetString(1);
            return new StoredDocument(reader.GetInt32(0), JsonDocument.Parse(raw).RootElement.Clone(), raw);
        }
        return (
            await ReadAsync("""SELECT "FilterSchemaVersion", "Filters"::text FROM "ADM_SearchPresets" WHERE "Id" = @id""", presetId),
            await ReadAsync("""SELECT "FilterSchemaVersion", "Requirements"::text FROM "OPS_Positions" WHERE "Id" = @id""", positionId));
    }

    private ApplicationDbContext NewContext() => new(
        new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(database.ConnectionString).UseTestFieldEncryption().Options);
}
