using KeplerTalento.Infrastructure.Encryption;
using KeplerTalento.Infrastructure.Persistence;
using KeplerTalento.Tools.DataMigration.Encryption;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Xunit;

namespace KeplerTalento.Tests.IntegrationTests;

/// <summary>
/// KTL-33 backfill, report and rotation, on a database that went through the production path:
/// plaintext written under the previous schema, then the encryption migration, then the tool.
/// </summary>
public sealed class EncryptionBackfillTests(PostgreSqlFixture database) : IClassFixture<PostgreSqlFixture>
{
    private const string BeforeEncryption = "20260925100450_AddPositionCandidates";

    [Fact]
    public async Task Backfill_encrypts_every_plaintext_value_and_a_rerun_changes_nothing()
    {
        var keys = await WriteKeysAsync();
        var ids = await SeedPlaintextAsync();

        var output = new StringWriter();
        Assert.Equal(0, await EncryptionCommands.RunAsync(["backfill", "--connection", database.ConnectionString, "--keys", keys], output, new StringWriter(), CancellationToken.None));
        Assert.Contains("CND_Candidates: 2 rows updated.", output.ToString(), StringComparison.Ordinal);

        // No seeded value survives anywhere in the encrypted columns.
        var stored = await ReadAllTextAsync();
        foreach (var value in new[] { "Lucía", "Fernández", "Lucia@Example.test", "600 123 456", "Notas privadas", "Consultora Sur", "Nota interna", "Lucía Fernández" })
        {
            Assert.DoesNotContain(value, stored, StringComparison.Ordinal);
        }

        var report = new StringWriter();
        Assert.Equal(0, await EncryptionCommands.RunAsync(["report", "--connection", database.ConnectionString, "--keys", keys], report, new StringWriter(), CancellationToken.None));
        Assert.Contains("every value is encrypted and readable", report.ToString(), StringComparison.Ordinal);
        Assert.Contains("CND_Candidates: 2 rows", report.ToString(), StringComparison.Ordinal);

        var rerun = new StringWriter();
        await EncryptionCommands.RunAsync(["backfill", "--connection", database.ConnectionString, "--keys", keys], rerun, new StringWriter(), CancellationToken.None);
        Assert.DoesNotContain("rows updated.", rerun.ToString().Replace(": 0 rows updated.", string.Empty, StringComparison.Ordinal), StringComparison.Ordinal);

        // The application reads the backfilled rows back with the same keys.
        await using var dbContext = NewContext(keys);
        var lucia = await dbContext.Candidates.AsNoTracking().SingleAsync(candidate => candidate.Id == ids.Lucia);
        Assert.Equal("Lucía", lucia.FirstName);
        Assert.Equal("Lucia@Example.test", lucia.Email);
        Assert.Equal("Consultora Sur", (await dbContext.CandidateExperience.AsNoTracking().SingleAsync()).Company);
        Assert.Equal("Nota interna", (await dbContext.CandidateNotes.AsNoTracking().SingleAsync()).Body);
        Assert.Contains("Lucía Fernández", (await dbContext.SearchPresets.AsNoTracking().SingleAsync()).Filters, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_report_fails_while_plaintext_remains()
    {
        var keys = await WriteKeysAsync();
        await SeedPlaintextAsync();

        var report = new StringWriter();
        var exit = await EncryptionCommands.RunAsync(["report", "--connection", database.ConnectionString, "--keys", keys], report, new StringWriter(), CancellationToken.None);

        Assert.Equal(4, exit);
        Assert.Contains("plaintext=2", report.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("Lucía", report.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Rotation_moves_every_value_to_the_new_keys_and_the_old_ones_can_then_be_retired()
    {
        var keys = await WriteKeysAsync();
        var ids = await SeedPlaintextAsync();
        await EncryptionCommands.RunAsync(["backfill", "--connection", database.ConnectionString, "--keys", keys], new StringWriter(), new StringWriter(), CancellationToken.None);

        Assert.Equal(0, await EncryptionCommands.RunAsync(["add-key", "encryption", "--keys", keys], new StringWriter(), new StringWriter(), CancellationToken.None));
        Assert.Equal(0, await EncryptionCommands.RunAsync(["add-key", "blindIndex", "--keys", keys], new StringWriter(), new StringWriter(), CancellationToken.None));

        // Mid-rotation, before the backfill: old values still read, and the import lookup still
        // finds the e-mail hashed under the previous blind-index key.
        await using (var midway = NewContext(keys))
        {
            Assert.Equal("Lucía", (await midway.Candidates.AsNoTracking().SingleAsync(candidate => candidate.Id == ids.Lucia)).FirstName);
            var index = new HmacBlindIndex(() => FieldKeySet.Load(keys));
            var hashes = index.ComputeAll("lucia@example.test");
            Assert.True(await midway.Candidates.AnyAsync(candidate =>
                hashes.Contains(EF.Property<string>(candidate, Infrastructure.Persistence.Configurations.CandidateConfiguration.EmailHash))));
        }

        await EncryptionCommands.RunAsync(["backfill", "--connection", database.ConnectionString, "--keys", keys], new StringWriter(), new StringWriter(), CancellationToken.None);
        var report = new StringWriter();
        Assert.Equal(0, await EncryptionCommands.RunAsync(["report", "--connection", database.ConnectionString, "--keys", keys], report, new StringWriter(), CancellationToken.None));
        Assert.DoesNotContain("e1=", report.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("b1=", report.ToString(), StringComparison.Ordinal);

        Assert.Equal(0, await EncryptionCommands.RunAsync(["retire-key", "e1", "--keys", keys], new StringWriter(), new StringWriter(), CancellationToken.None));
        Assert.Equal(0, await EncryptionCommands.RunAsync(["retire-key", "b1", "--keys", keys], new StringWriter(), new StringWriter(), CancellationToken.None));
        Assert.Equal(3, await EncryptionCommands.RunAsync(["retire-key", "e2", "--keys", keys], new StringWriter(), new StringWriter(), CancellationToken.None));
        Assert.Equal(0, await EncryptionCommands.RunAsync(["report", "--connection", database.ConnectionString, "--keys", keys], new StringWriter(), new StringWriter(), CancellationToken.None));
    }

    [Fact]
    public async Task Generate_keys_refuses_to_overwrite_an_existing_file()
    {
        var path = await WriteKeysAsync();

        var error = new StringWriter();
        Assert.Equal(3, await EncryptionCommands.RunAsync(["generate-keys", "--out", path], new StringWriter(), error, CancellationToken.None));
        Assert.DoesNotContain(path, error.ToString(), StringComparison.Ordinal);
    }

    private sealed record SeededIds(Guid Lucia, Guid Pablo);

    /// <summary>Plaintext under the previous schema, then the encryption migration on top.</summary>
    private async Task<SeededIds> SeedPlaintextAsync()
    {
        await using (var reset = NewContext(TestFieldEncryption.KeyFile))
        {
            await reset.Database.EnsureDeletedAsync();
            await reset.GetService<IMigrator>().MigrateAsync(BeforeEncryption);
        }

        var lucia = Guid.CreateVersion7();
        var pablo = Guid.CreateVersion7();
        await using (var connection = new NpgsqlConnection(database.ConnectionString))
        {
            await connection.OpenAsync();
            // The sector is written in the historical schema's own terms: the current EF model may
            // carry catalog columns added after the encryption migration (KTL-41 added "Color").
            await using var command = new NpgsqlCommand(
                """
                INSERT INTO "CAT_CatalogItems" ("Id", "Family", "Code", "NameEs", "NameNormalized", "SortOrder", "IsActive", "CreatedAtUtc", "UpdatedAtUtc")
                VALUES (gen_random_uuid(), 'sector', 'SERVICIOS', 'Servicios', 'servicios', 1, true, now(), now());
                INSERT INTO "CND_Candidates" ("Id", "FirstName", "LastName", "Phone", "Email", "Notes", "Status", "IsActive", "CreatedAtUtc", "UpdatedAtUtc")
                VALUES (@lucia, 'Lucía', 'Fernández', '600 123 456', 'Lucia@Example.test', 'Notas privadas', 'available', true, now(), now()),
                       (@pablo, 'Pablo', 'Ruiz', '', 'pablo@example.test', '', 'available', true, now(), now());
                INSERT INTO "CND_CandidateExperience" ("Id", "CandidateId", "SectorId", "SectorFamily", "Company", "Position", "IsCurrent")
                SELECT gen_random_uuid(), @lucia, "Id", "Family", 'Consultora Sur', 'Analista', false
                FROM "CAT_CatalogItems" WHERE "Family" = 'sector' LIMIT 1;
                INSERT INTO "CND_CandidateNotes" ("Id", "CandidateId", "Body", "CreatedAtUtc", "UpdatedAtUtc", "IsActive")
                VALUES (gen_random_uuid(), @lucia, 'Nota interna', now(), now(), true);
                INSERT INTO "ADM_SearchPresets" ("Id", "Name", "NormalizedName", "Filters", "FilterSchemaVersion", "CreatedAtUtc", "UpdatedAtUtc")
                VALUES (gen_random_uuid(), 'Perfil analista', 'perfil analista', jsonb_build_object('version', 1, 'text', 'Lucía Fernández'), 1, now(), now());
                """,
                connection);
            command.Parameters.AddWithValue("lucia", lucia);
            command.Parameters.AddWithValue("pablo", pablo);
            await command.ExecuteNonQueryAsync();
        }

        await using (var migrate = NewContext(TestFieldEncryption.KeyFile))
        {
            await DatabaseInitializer.MigrateAsync(migrate, CancellationToken.None);
            await DatabaseInitializer.SeedCatalogsAsync(migrate, CancellationToken.None);
        }
        return new SeededIds(lucia, pablo);
    }

    private async Task<string> ReadAllTextAsync()
    {
        await using var connection = new NpgsqlConnection(database.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            """
            SELECT string_agg(t::text, ' ') FROM (
                SELECT row_to_json(c)::text AS t FROM "CND_Candidates" c
                UNION ALL SELECT row_to_json(e)::text FROM "CND_CandidateExperience" e
                UNION ALL SELECT row_to_json(n)::text FROM "CND_CandidateNotes" n
                UNION ALL SELECT row_to_json(p)::text FROM "ADM_SearchPresets" p
            ) rows(t)
            """,
            connection);
        return (string)(await command.ExecuteScalarAsync())!;
    }

    private static async Task<string> WriteKeysAsync()
    {
        var path = Path.Combine(Path.GetTempPath(), $"ktl-backfill-keys-{Guid.NewGuid():N}.json");
        var output = new StringWriter();
        Assert.Equal(0, await EncryptionCommands.RunAsync(["generate-keys", "--out", path], output, new StringWriter(), CancellationToken.None));
        return path;
    }

    private ApplicationDbContext NewContext(string keyFile)
    {
        var keys = new Lazy<FieldKeySet>(() => FieldKeySet.Load(keyFile));
        return new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseNpgsql(database.ConnectionString)
                .UseFieldEncryption(new AesGcmFieldProtector(() => keys.Value), new HmacBlindIndex(() => keys.Value))
                .Options);
    }
}
