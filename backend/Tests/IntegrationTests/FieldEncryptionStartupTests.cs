using KeplerTalento.Application.Abstractions.Encryption;
using KeplerTalento.Domain.Candidates;
using KeplerTalento.Infrastructure.Encryption;
using KeplerTalento.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace KeplerTalento.Tests.IntegrationTests;

/// <summary>
/// KTL-33: the API fails closed when encryption cannot be trusted, and the migrator does not
/// need the keys at all.
/// </summary>
[Collection(WebHostCollection.Name)]
public sealed class FieldEncryptionStartupTests(PostgreSqlFixture database) : IClassFixture<PostgreSqlFixture>
{
    public static TheoryData<string, string> BrokenKeyFiles()
    {
        object Ring(string active, string id, byte[] key) =>
            new { active, keys = new Dictionary<string, string> { [id] = Convert.ToBase64String(key) } };
        string File(object encryption, object blindIndex) =>
            System.Text.Json.JsonSerializer.Serialize(new { encryption, blindIndex });
        var shared = FieldKeySet.NewKey();
        return new TheoryData<string, string>
        {
            { "not json at all", FieldKeyException.Malformed },
            { File(Ring("e1", "e1", new byte[16]), Ring("b1", "b1", FieldKeySet.NewKey())), FieldKeyException.WrongLength },
            { File(Ring("e1", "e1", shared), Ring("b1", "b1", shared)), FieldKeyException.SharedKey },
            { File(Ring("e9", "e1", FieldKeySet.NewKey()), Ring("b1", "b1", FieldKeySet.NewKey())), FieldKeyException.UnknownActive },
        };
    }

    [Fact]
    public async Task The_api_refuses_to_start_without_its_key_file()
    {
        await ResetAsync();
        var missing = Path.Combine(Path.GetTempPath(), $"ktl-no-keys-{Guid.NewGuid():N}.json");

        var failure = await StartFailureAsync(missing);

        var keyFailure = Assert.IsType<FieldKeyException>(failure);
        Assert.Equal(FieldKeyException.Missing, keyFailure.Code);
        Assert.DoesNotContain(missing, keyFailure.Message, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(BrokenKeyFiles))]
    public async Task The_api_refuses_to_start_with_a_broken_key_file(string contents, string code)
    {
        await ResetAsync();
        var path = Path.Combine(Path.GetTempPath(), $"ktl-broken-keys-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(path, contents);
        try
        {
            var failure = await StartFailureAsync(path);

            Assert.Equal(code, Assert.IsType<FieldKeyException>(failure).Code);
            Assert.DoesNotContain("base64", failure.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task The_api_refuses_to_start_while_a_candidate_value_is_still_plaintext()
    {
        await ResetAsync();
        await using (var dbContext = NewDbContext())
        {
            var candidate = new Candidate(Guid.CreateVersion7(), "Ana", "López", DateTimeOffset.UtcNow);
            dbContext.Candidates.Add(candidate);
            await dbContext.SaveChangesAsync();
            // What a database looks like after the schema migration and before the backfill.
            await dbContext.Database.ExecuteSqlRawAsync(
                """
                ALTER TABLE "CND_Candidates" DROP CONSTRAINT "CK_CND_Candidates_LastName_Encrypted";
                UPDATE "CND_Candidates" SET "LastName" = 'López';
                """);
        }

        try
        {
            var failure = Assert.IsType<FieldEncryptionStartupException>(await StartFailureAsync(TestFieldEncryption.KeyFile));

            Assert.Equal(FieldEncryptionStartupCheck.PlaintextPresent, failure.Code);
            Assert.Equal("CND_Candidates", failure.Table);
            Assert.DoesNotContain("López", failure.Message, StringComparison.Ordinal);
        }
        finally
        {
            await ResetAsync(recreate: true);
        }
    }

    [Fact]
    public async Task The_api_refuses_to_start_while_a_saved_search_term_is_still_plaintext()
    {
        await ResetAsync();
        await using (var dbContext = NewDbContext())
        {
            await dbContext.Database.ExecuteSqlRawAsync(
                """
                INSERT INTO "ADM_SearchPresets" ("Id", "Name", "NormalizedName", "Filters", "FilterSchemaVersion", "CreatedAtUtc", "UpdatedAtUtc", "Version")
                SELECT gen_random_uuid(), 'Antigua', 'antigua', jsonb_build_object('version', 2, 'text', 'Ana López'), 2, now(), now(), 1
                """);
        }

        var failure = Assert.IsType<FieldEncryptionStartupException>(await StartFailureAsync(TestFieldEncryption.KeyFile));

        Assert.Equal(FieldEncryptionStartupCheck.PlaintextPresent, failure.Code);
        Assert.Equal("ADM_SearchPresets", failure.Table);
    }

    [Fact]
    public async Task The_api_refuses_to_start_while_the_legacy_staging_schema_exists()
    {
        await ResetAsync();
        await using (var dbContext = NewDbContext())
        {
            await dbContext.Database.ExecuteSqlRawAsync("CREATE SCHEMA migration_staging;");
        }

        try
        {
            var failure = Assert.IsType<FieldEncryptionStartupException>(await StartFailureAsync(TestFieldEncryption.KeyFile));

            Assert.Equal(FieldEncryptionStartupCheck.StagingPresent, failure.Code);
        }
        finally
        {
            await using var dbContext = NewDbContext();
            await dbContext.Database.ExecuteSqlRawAsync("DROP SCHEMA IF EXISTS migration_staging;");
        }
    }

    [Fact]
    public async Task The_api_starts_on_an_encrypted_database_with_its_keys()
    {
        await ResetAsync();
        await using (var dbContext = NewDbContext())
        {
            dbContext.Candidates.Add(new Candidate(Guid.CreateVersion7(), "Ana", "López", DateTimeOffset.UtcNow));
            await dbContext.SaveChangesAsync();
        }

        using var factory = CreateFactory(TestFieldEncryption.KeyFile);
        using var client = factory.CreateClient();
        var response = await client.GetAsync("/api/health/live");

        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Migrations_and_seeding_run_without_any_key()
    {
        // The migrator is the API image run with --migrate and is never given the keys. Its whole
        // workload — schema, catalogs, the bootstrap administrator — must not touch them.
        var noKeys = new AesGcmFieldProtector(() => throw new FieldKeyException(FieldKeyException.Missing, "missing"));
        var noBlindIndex = new HmacBlindIndex(() => throw new FieldKeyException(FieldKeyException.Missing, "missing"));
        await using (var reset = NewDbContext())
        {
            await reset.Database.EnsureDeletedAsync();
        }
        await using var dbContext = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseNpgsql(database.ConnectionString)
                .UseFieldEncryption(noKeys, noBlindIndex)
                .Options);

        await DatabaseInitializer.MigrateAsync(dbContext, CancellationToken.None);
        await DatabaseInitializer.SeedCatalogsAsync(dbContext, CancellationToken.None);
        await DatabaseInitializer.SeedBootstrapAdministratorAsync(
            dbContext, "admin@example.test", "Administración", CancellationToken.None);

        Assert.Empty(await dbContext.Database.GetPendingMigrationsAsync());
    }

    private async Task ResetAsync(bool recreate = false)
    {
        await using var dbContext = NewDbContext();
        if (recreate)
        {
            await dbContext.Database.EnsureDeletedAsync();
        }
        await DatabaseInitializer.MigrateAsync(dbContext, CancellationToken.None);
        await DatabaseInitializer.SeedCatalogsAsync(dbContext, CancellationToken.None);
        await dbContext.SearchPresets.ExecuteDeleteAsync();
        await dbContext.Candidates.ExecuteDeleteAsync();
    }

    private async Task<Exception> StartFailureAsync(string keyFile)
    {
        using var factory = CreateFactory(keyFile);
        return await Assert.ThrowsAnyAsync<Exception>(() =>
        {
            using var client = factory.CreateClient();
            return Task.CompletedTask;
        });
    }

    private ApplicationDbContext NewDbContext() => new(
        new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(database.ConnectionString).UseTestFieldEncryption().Options);

    private WebApplicationFactory<Program> CreateFactory(string keyFile)
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
                    [$"{FieldEncryptionOptions.SectionName}:KeyFile"] = keyFile,
                }));
        });
    }
}
