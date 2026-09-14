using KeplerTalento.Application.Features.Search;
using KeplerTalento.Domain.Search;
using KeplerTalento.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Xunit;

namespace KeplerTalento.Tests.IntegrationTests;

/// <summary>
/// Database-level evidence for the saved-search table as KTL-10 created it and KTL-14 shared
/// it: what the table enforces, which indexes exist, what the runtime role may do, that the
/// shared-library migration discards owner-scoped rows, and that deploying twice changes
/// nothing.
/// </summary>
public sealed class SearchSchemaTests(PostgreSqlFixture database) : IClassFixture<PostgreSqlFixture>
{
    private const string Table = "ADM_SearchPresets";

    /// <summary>The last migration before KTL-14 reshaped the table.</summary>
    private const string BeforeSharedLibrary = "20260910170641_EnforceCandidateRelationUniqueness";

    [Fact]
    public async Task Deploying_the_migration_twice_leaves_the_same_schema()
    {
        await MigrateAsync();
        var first = await DescribeAsync();

        // Re-running is what a redeployment does. EF's migration history makes it a no-op;
        // this proves the no-op, rather than assuming it.
        await MigrateAsync();
        var second = await DescribeAsync();

        Assert.Equal(first, second);
    }

    [Fact]
    public async Task The_preset_table_carries_its_documented_shape()
    {
        await MigrateAsync();

        var columns = await QueryAsync(
            """
            SELECT column_name || ' ' || data_type || ' ' || is_nullable
            FROM information_schema.columns
            WHERE table_schema = 'public' AND table_name = @table
            ORDER BY column_name
            """);

        // No owner column: the library is shared and nothing records who wrote a preset.
        Assert.Equal(
            [
                "CreatedAtUtc timestamp with time zone NO",
                "FilterSchemaVersion integer NO",
                "Filters jsonb NO",
                "Id uuid NO",
                "LastUsedAtUtc timestamp with time zone YES",
                "Name character varying NO",
                "NormalizedName character varying NO",
                "UpdatedAtUtc timestamp with time zone NO",
                "Version integer NO",
            ],
            columns);
    }

    [Fact]
    public async Task The_preset_table_carries_its_documented_constraints_and_index()
    {
        await MigrateAsync();

        var checks = await QueryAsync(
            """
            SELECT conname || ' ' || pg_get_constraintdef(oid) FROM pg_constraint
            WHERE conrelid = format('%I', @table)::regclass AND contype = 'c'
            ORDER BY conname
            """);
        var indexes = await QueryAsync(
            """
            SELECT indexname FROM pg_indexes
            WHERE schemaname = 'public' AND tablename = @table
            ORDER BY indexname
            """);

        Assert.Equal(
            [
                $"CK_{Table}_FilterSchemaVersion",
                $"CK_{Table}_Filters",
                $"CK_{Table}_Name",
                $"CK_{Table}_Timestamps",
                $"CK_{Table}_Version",
            ],
            checks.Select(check => check.Split(' ')[0]));
        // Applying a preset records its use without touching its update time, so the check
        // must allow "last used" to be later than "last updated" — only not before creation.
        var timestamps = checks.Single(check => check.StartsWith($"CK_{Table}_Timestamps", StringComparison.Ordinal));
        Assert.Contains("\"LastUsedAtUtc\" >= \"CreatedAtUtc\"", timestamps, StringComparison.Ordinal);
        Assert.DoesNotContain("\"LastUsedAtUtc\" <= \"UpdatedAtUtc\"", timestamps, StringComparison.Ordinal);
        // The unique index is also the listing index; a second index over the same column
        // would cost writes and buy nothing.
        Assert.Equal(
            [$"PK_{Table}", "UX_ADM_SearchPresets_NormalizedName"],
            indexes);
    }

    [Fact]
    public async Task Search_relies_on_indexes_that_already_existed_rather_than_adding_duplicates()
    {
        await MigrateAsync();

        var indexes = await QueryAsync(
            """
            SELECT indexname FROM pg_indexes
            WHERE schemaname = 'public'
              AND indexname IN (
                'IX_CND_Candidates_IsActive_UpdatedAtUtc',
                'IX_CND_CandidateSkills_SkillId_SkillFamily',
                'IX_CND_CandidateLanguages_LanguageId_LanguageFamily',
                'IX_CND_CandidatePrograms_ProgramId_ProgramFamily',
                'UX_CAT_CatalogItems_Family_NameNormalized',
                'UX_CND_Documents_CandidateId_Primary')
            ORDER BY indexname
            """);

        // Every predicate search issues is served by one of these, all of them owned by
        // KTL-6 through KTL-9. KTL-10 adds none of its own: wider relation indexes were
        // measured against the KTL-7-scale dataset and the planner never chose them.
        Assert.Equal(6, indexes.Count);
        var added = await QueryAsync(
            """
            SELECT indexname FROM pg_indexes
            WHERE schemaname = 'public' AND indexname LIKE '%CandidateId_LevelId'
            """);
        Assert.Empty(added);
    }

    [Fact]
    public async Task No_full_text_extension_is_installed_by_this_slice()
    {
        await MigrateAsync();

        var extensions = await QueryAsync("SELECT extname FROM pg_extension WHERE extname = 'pg_trgm'");

        // KTL-10 deliberately ships no trigram index: at the documented scale a parameterized
        // scan is cheaper than the extension's operational cost, and adding one would need
        // the design amended first.
        Assert.Empty(extensions);
    }

    [Fact]
    public async Task The_runtime_role_may_only_do_what_saved_searches_need()
    {
        await MigrateAsync();

        // Exactly the four verbs KTL-10 granted, which KTL-14 deliberately did not broaden.
        Assert.Equal(
            ["DELETE", "INSERT", "SELECT", "UPDATE"],
            await QueryAsync(
                """
                SELECT privilege_type FROM information_schema.role_table_grants
                WHERE table_name = @table AND grantee = 'ktl_runtime'
                ORDER BY privilege_type
                """));

        await using var connection = new NpgsqlConnection(database.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            """
            SELECT
                has_table_privilege('ktl_runtime', '"ADM_SearchPresets"', 'TRUNCATE'),
                has_schema_privilege('ktl_runtime', 'public', 'CREATE'),
                pg_has_role('ktl_runtime', 'pg_read_all_data', 'MEMBER'),
                (SELECT rolsuper OR rolcreaterole OR rolcreatedb FROM pg_roles WHERE rolname = 'ktl_runtime')
            """,
            connection);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());

        // Nothing beyond what a saved search needs: no bulk removal, no DDL, no blanket read
        // of the cluster, no role or database creation.
        Assert.False(reader.GetBoolean(0));
        Assert.False(reader.GetBoolean(1));
        Assert.False(reader.GetBoolean(2));
        Assert.False(reader.GetBoolean(3));
    }

    [Theory]
    [InlineData("Duplicada", "DUPLICADA")]
    [InlineData("Inglés B2", "ingles b2")]
    public async Task The_database_refuses_a_second_preset_with_the_same_folded_name(string first, string second)
    {
        await MigrateAsync();
        await using var dbContext = NewContext();
        await dbContext.SearchPresets.ExecuteDeleteAsync();
        var filters = SearchFilterDocument.Serialize(SearchFilterNormalization.Normalize(null, "Filters"));
        var now = DateTimeOffset.UtcNow;
        dbContext.SearchPresets.Add(new SearchPreset(Guid.CreateVersion7(), first, filters, 1, now));
        await dbContext.SaveChangesAsync();

        dbContext.SearchPresets.Add(new SearchPreset(Guid.CreateVersion7(), second, filters, 1, now));
        var failure = await Assert.ThrowsAsync<DbUpdateException>(() => dbContext.SaveChangesAsync());

        Assert.Equal(
            PostgresErrorCodes.UniqueViolation,
            ((PostgresException)failure.InnerException!).SqlState);
    }

    [Fact]
    public async Task A_last_used_time_later_than_the_update_time_is_accepted()
    {
        await MigrateAsync();
        await using var dbContext = NewContext();
        await dbContext.SearchPresets.ExecuteDeleteAsync();
        var filters = SearchFilterDocument.Serialize(SearchFilterNormalization.Normalize(null, "Filters"));
        var preset = new SearchPreset(Guid.CreateVersion7(), "Usada", filters, 1, DateTimeOffset.UtcNow);
        dbContext.SearchPresets.Add(preset);
        await dbContext.SaveChangesAsync();

        preset.MarkUsed(DateTimeOffset.UtcNow.AddMinutes(5));
        await dbContext.SaveChangesAsync();

        var stored = await NewContext().SearchPresets.AsNoTracking().SingleAsync();
        Assert.True(stored.LastUsedAtUtc > stored.UpdatedAtUtc);
        Assert.Equal(1, stored.Version);
    }

    [Fact]
    public async Task The_shared_library_migration_discards_every_owner_scoped_preset()
    {
        await MigrateAsync();
        await using (var dbContext = NewContext())
        {
            // Back to the owner-scoped shape KTL-10 deployed, holding presets from two owners —
            // including two whose names collide once owners no longer separate them.
            await dbContext.GetService<IMigrator>().MigrateAsync(BeforeSharedLibrary);
            await dbContext.Database.ExecuteSqlRawAsync(
                """
                INSERT INTO "ADM_SearchPresets"
                    ("Id", "OwnerId", "Name", "NormalizedName", "Filters", "FilterSchemaVersion", "CreatedAtUtc", "UpdatedAtUtc")
                VALUES
                    (gen_random_uuid(), 'owner-a', 'Candidatos de Marta', 'candidatos de marta', '{{"text":"Marta"}}', 1, now(), now()),
                    (gen_random_uuid(), 'owner-a', 'Compartida', 'compartida', '{{}}', 1, now(), now()),
                    (gen_random_uuid(), 'owner-b', 'compartida', 'compartida', '{{}}', 1, now(), now())
                """);
        }

        await MigrateAsync();

        Assert.Equal(["0"], await QueryAsync("""SELECT count(*)::text FROM "ADM_SearchPresets" """));
        Assert.Empty(await QueryAsync(
            """
            SELECT column_name FROM information_schema.columns
            WHERE table_schema = 'public' AND table_name = @table AND column_name = 'OwnerId'
            """));
    }

    private DbContextOptions<ApplicationDbContext> Options =>
        new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(database.ConnectionString).Options;

    private ApplicationDbContext NewContext() => new(Options);

    private async Task MigrateAsync()
    {
        await using var dbContext = NewContext();
        await DatabaseInitializer.MigrateAsync(dbContext, CancellationToken.None);
    }

    /// <summary>Everything about the table that a second deployment must not change.</summary>
    private async Task<IReadOnlyList<string>> DescribeAsync() =>
    [
        .. await QueryAsync(
            """
            SELECT column_name || ':' || data_type || ':' || is_nullable
            FROM information_schema.columns
            WHERE table_schema = 'public' AND table_name = @table
            ORDER BY column_name
            """),
        .. await QueryAsync(
            """
            SELECT 'index:' || indexdef FROM pg_indexes
            WHERE schemaname = 'public' AND tablename = @table
            ORDER BY indexname
            """),
        .. await QueryAsync(
            """
            SELECT 'check:' || conname || ':' || pg_get_constraintdef(oid) FROM pg_constraint
            WHERE conrelid = format('%I', @table)::regclass
            ORDER BY conname
            """),
        .. await QueryAsync(
            """
            SELECT 'grant:' || privilege_type FROM information_schema.role_table_grants
            WHERE table_name = @table AND grantee = 'ktl_runtime'
            ORDER BY privilege_type
            """),
    ];

    private async Task<IReadOnlyList<string>> QueryAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(database.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("table", Table);
        await using var reader = await command.ExecuteReaderAsync();
        var rows = new List<string>();
        while (await reader.ReadAsync())
        {
            rows.Add(reader.GetString(0));
        }
        return rows;
    }
}
