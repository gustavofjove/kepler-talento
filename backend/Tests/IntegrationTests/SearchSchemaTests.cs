using KeplerTalento.Application.Features.Search;
using KeplerTalento.Domain.Search;
using KeplerTalento.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace KeplerTalento.Tests.IntegrationTests;

/// <summary>
/// Database-level evidence for the KTL-10 migration: what the table enforces, which indexes
/// exist, what the runtime role may do, and that deploying twice changes nothing.
/// </summary>
public sealed class SearchSchemaTests(PostgreSqlFixture database) : IClassFixture<PostgreSqlFixture>
{
    private const string Table = "ADM_SearchPresets";

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

        Assert.Equal(
            [
                "CreatedAtUtc timestamp with time zone NO",
                "FilterSchemaVersion integer NO",
                "Filters jsonb NO",
                "Id uuid NO",
                "LastUsedAtUtc timestamp with time zone YES",
                "Name character varying NO",
                "NormalizedName character varying NO",
                "OwnerId character varying NO",
                "UpdatedAtUtc timestamp with time zone NO",
            ],
            columns);
    }

    [Fact]
    public async Task The_preset_table_carries_its_documented_constraints_and_index()
    {
        await MigrateAsync();

        var checks = await QueryAsync(
            """
            SELECT conname FROM pg_constraint
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
                $"CK_{Table}_Owner",
                $"CK_{Table}_Timestamps",
            ],
            checks);
        // The unique index is also the listing index; a second index over the same columns
        // would cost writes and buy nothing.
        Assert.Equal(
            [$"PK_{Table}", "UX_ADM_SearchPresets_Owner_NormalizedName"],
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

        await using var connection = new NpgsqlConnection(database.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            """
            SELECT
                has_table_privilege('ktl_runtime', '"ADM_SearchPresets"', 'SELECT'),
                has_table_privilege('ktl_runtime', '"ADM_SearchPresets"', 'INSERT'),
                has_table_privilege('ktl_runtime', '"ADM_SearchPresets"', 'UPDATE'),
                has_table_privilege('ktl_runtime', '"ADM_SearchPresets"', 'DELETE'),
                has_table_privilege('ktl_runtime', '"ADM_SearchPresets"', 'TRUNCATE'),
                has_schema_privilege('ktl_runtime', 'public', 'CREATE'),
                pg_has_role('ktl_runtime', 'pg_read_all_data', 'MEMBER'),
                (SELECT rolsuper OR rolcreaterole OR rolcreatedb FROM pg_roles WHERE rolname = 'ktl_runtime')
            """,
            connection);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());

        Assert.True(reader.GetBoolean(0));
        Assert.True(reader.GetBoolean(1));
        Assert.True(reader.GetBoolean(2));
        Assert.True(reader.GetBoolean(3));
        // Everything a saved search needs, and nothing beyond it: no bulk removal, no DDL,
        // no blanket read of the cluster, no role or database creation.
        Assert.False(reader.GetBoolean(4));
        Assert.False(reader.GetBoolean(5));
        Assert.False(reader.GetBoolean(6));
        Assert.False(reader.GetBoolean(7));
    }

    [Fact]
    public async Task The_database_refuses_a_second_preset_with_the_same_owner_and_folded_name()
    {
        await MigrateAsync();
        await using var dbContext = NewContext();
        await dbContext.SearchPresets.ExecuteDeleteAsync();
        var filters = SearchFilterDocument.Serialize(SearchFilterNormalization.Normalize(null, "Filters"));
        var now = DateTimeOffset.UtcNow;
        dbContext.SearchPresets.Add(new SearchPreset(Guid.CreateVersion7(), "owner", "Duplicada", filters, 1, now));
        await dbContext.SaveChangesAsync();

        dbContext.SearchPresets.Add(new SearchPreset(Guid.CreateVersion7(), "owner", "DUPLICADA", filters, 1, now));
        var failure = await Assert.ThrowsAsync<DbUpdateException>(() => dbContext.SaveChangesAsync());

        Assert.Equal(
            PostgresErrorCodes.UniqueViolation,
            ((PostgresException)failure.InnerException!).SqlState);
    }

    [Fact]
    public async Task The_same_folded_name_is_free_for_a_different_owner()
    {
        await MigrateAsync();
        await using var dbContext = NewContext();
        await dbContext.SearchPresets.ExecuteDeleteAsync();
        var filters = SearchFilterDocument.Serialize(SearchFilterNormalization.Normalize(null, "Filters"));
        var now = DateTimeOffset.UtcNow;

        dbContext.SearchPresets.Add(new SearchPreset(Guid.CreateVersion7(), "owner-a", "Compartida", filters, 1, now));
        dbContext.SearchPresets.Add(new SearchPreset(Guid.CreateVersion7(), "owner-b", "compartida", filters, 1, now));
        await dbContext.SaveChangesAsync();

        Assert.Equal(2, await dbContext.SearchPresets.CountAsync());
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
