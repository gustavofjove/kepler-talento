using KeplerTalento.Application.Abstractions.Correlation;
using KeplerTalento.Application.Abstractions.Identity;
using KeplerTalento.Application.Abstractions.Persistence;
using KeplerTalento.Application.Features.Search;
using KeplerTalento.Domain.Positions;
using KeplerTalento.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace KeplerTalento.Tests.IntegrationTests;

/// <summary>
/// Database-level evidence for <c>OPS_Positions</c>: what the table enforces on its own, the
/// indexes the list relies on, how a concurrent title race and a stale version surface, the
/// seeded role permissions, and exactly what the runtime role may do.
/// </summary>
public sealed class PositionSchemaTests(PostgreSqlFixture database) : IClassFixture<PostgreSqlFixture>
{
    private const string Table = "OPS_Positions";
    private static readonly string EmptyRequirements =
        SearchFilterDocument.Serialize(SearchFilterNormalization.Normalize(null, "Requirements"));

    [Fact]
    public async Task The_position_table_carries_its_documented_constraints_and_indexes()
    {
        await ResetAsync();

        var checks = await QueryAsync(
            """
            SELECT conname FROM pg_constraint
            WHERE conrelid = format('%I', @table)::regclass AND contype = 'c'
            ORDER BY conname
            """);
        var indexes = await QueryAsync(
            "SELECT indexname FROM pg_indexes WHERE schemaname = 'public' AND tablename = @table ORDER BY indexname");

        Assert.Equal(
            [
                "CK_OPS_Positions_Description",
                "CK_OPS_Positions_FilterSchemaVersion",
                "CK_OPS_Positions_Location",
                "CK_OPS_Positions_Requirements",
                "CK_OPS_Positions_RequirementsVersion",
                "CK_OPS_Positions_Status",
                "CK_OPS_Positions_Timestamps",
                "CK_OPS_Positions_Title",
            ],
            checks);
        Assert.Equal(
            [
                "IX_OPS_Positions_NormalizedLocation_Trgm",
                "IX_OPS_Positions_NormalizedTitle_Trgm",
                "IX_OPS_Positions_Status_NormalizedLocation_Id",
                "IX_OPS_Positions_Status_NormalizedTitle_Id",
                "IX_OPS_Positions_Status_UpdatedAtUtc_Id",
                "PK_OPS_Positions",
                "UX_OPS_Positions_NormalizedTitle",
            ],
            indexes);
        Assert.Equal(["pg_trgm"], await QueryAsync("SELECT extname FROM pg_extension WHERE extname = 'pg_trgm' AND @table IS NOT NULL"));
    }

    [Theory]
    [InlineData("\"Status\" = 'archived'", "CK_OPS_Positions_Status")]
    [InlineData("\"Title\" = '   '", "CK_OPS_Positions_Title")]
    [InlineData("\"Requirements\" = '[]'::jsonb", "CK_OPS_Positions_Requirements")]
    [InlineData("\"Requirements\" = '{\"version\": 2}'::jsonb", "CK_OPS_Positions_RequirementsVersion")]
    [InlineData("\"FilterSchemaVersion\" = 0, \"Requirements\" = '{\"version\": 0}'::jsonb", "CK_OPS_Positions_FilterSchemaVersion")]
    [InlineData("\"UpdatedAtUtc\" = \"CreatedAtUtc\" - interval '1 day'", "CK_OPS_Positions_Timestamps")]
    public async Task The_table_refuses_rows_the_application_would_never_write(string assignment, string constraint)
    {
        await ResetAsync();
        var id = await InsertAsync("Válida");

        await using var connection = await OpenAsync();
        await using var command = new NpgsqlCommand($"UPDATE \"{Table}\" SET {assignment} WHERE \"Id\" = @id", connection);
        command.Parameters.AddWithValue("id", id);
        var failure = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());

        Assert.Equal(PostgresErrorCodes.CheckViolation, failure.SqlState);
        Assert.Equal(constraint, failure.ConstraintName);
    }

    [Fact]
    public async Task Titles_differing_only_by_case_and_accents_cannot_both_be_stored_even_concurrently()
    {
        await ResetAsync();

        async Task<Exception?> SaveAsync(string title)
        {
            await using var dbContext = NewDbContext();
            dbContext.Positions.Add(NewPosition(title));
            try
            {
                await dbContext.SaveChangesAsync();
                return null;
            }
            catch (DbUpdateException exception)
            {
                return exception;
            }
        }

        var outcomes = await Task.WhenAll(SaveAsync("Programador sénior"), SaveAsync("programador SENIOR"));

        var failure = Assert.Single(outcomes, outcome => outcome is not null);
        Assert.Equal(PostgresErrorCodes.UniqueViolation, ((PostgresException)failure!.InnerException!).SqlState);
        await using var verify = NewDbContext();
        Assert.Equal(1, await verify.Positions.CountAsync());
    }

    [Fact]
    public async Task The_repository_reports_a_title_race_and_a_stale_version_as_distinct_outcomes()
    {
        await ResetAsync();
        await using (var first = NewDbContext())
        {
            first.Positions.Add(NewPosition("Programador sénior"));
            await first.SaveChangesAsync();
        }

        await using (var racing = NewDbContext())
        {
            var repository = NewRepository(racing);
            repository.Add(NewPosition("PROGRAMADOR SENIOR"));
            Assert.Equal(PositionSaveOutcome.TitleConflict, await repository.SaveAsync(PositionAuditEvents.Created, CancellationToken.None));
        }

        await using var stale = NewDbContext();
        var staleRepository = NewRepository(stale);
        var position = (await staleRepository.FindAsync((await stale.Positions.AsNoTracking().SingleAsync()).Id, CancellationToken.None))!;
        var readVersion = position.Version;
        Assert.NotEqual(0u, readVersion);

        await using (var winner = NewDbContext())
        {
            var winning = await winner.Positions.SingleAsync();
            winning.Update("Programador sénior", string.Empty, "Bilbao", PositionStatuses.Open, winning.Requirements, 1, DateTimeOffset.UtcNow);
            await winner.SaveChangesAsync();
        }

        staleRepository.ExpectVersion(position, readVersion);
        position.Update("Programador sénior", string.Empty, "Sevilla", PositionStatuses.Closed, position.Requirements, 1, DateTimeOffset.UtcNow);
        Assert.Equal(PositionSaveOutcome.ConcurrencyConflict, await staleRepository.SaveAsync(PositionAuditEvents.StatusChanged, CancellationToken.None));

        await using var verify = NewDbContext();
        var stored = await verify.Positions.AsNoTracking().SingleAsync();
        Assert.Equal("Bilbao", stored.Location);
        Assert.Equal(PositionStatuses.Open, stored.Status);
        Assert.NotEqual(readVersion, stored.Version);
    }

    [Fact]
    public async Task A_saved_write_reloads_the_new_xmin_version()
    {
        await ResetAsync();
        await using var dbContext = NewDbContext();
        var repository = NewRepository(dbContext);
        var position = NewPosition("Analista");
        repository.Add(position);

        Assert.Equal(PositionSaveOutcome.Saved, await repository.SaveAsync(PositionAuditEvents.Created, CancellationToken.None));
        var created = position.Version;
        position.Update("Analista", string.Empty, string.Empty, PositionStatuses.Closed, position.Requirements, 1, DateTimeOffset.UtcNow);
        Assert.Equal(PositionSaveOutcome.Saved, await repository.SaveAsync(PositionAuditEvents.StatusChanged, CancellationToken.None));

        Assert.NotEqual(0u, created);
        Assert.NotEqual(created, position.Version);
        Assert.Equal(
            [PositionAuditEvents.Created, PositionAuditEvents.StatusChanged],
            await dbContext.AuditEvents.AsNoTracking().OrderBy(e => e.CreatedAtUtc).Select(e => e.EventType).ToListAsync());
    }

    [Fact]
    public async Task Seeding_position_permissions_twice_changes_nothing_and_leaves_custom_roles_alone()
    {
        await ResetAsync();
        await using (var dbContext = NewDbContext())
        {
            dbContext.Roles.Add(new Domain.Identity.Role(
                Guid.CreateVersion7(), "custom_viewer", "Custom", false, ["candidates.read"], DateTimeOffset.UtcNow));
            await dbContext.SaveChangesAsync();
        }
        await using var connection = await OpenAsync();
        var before = await QueryAsync("""SELECT "Name" || '=' || "Permissions"::text FROM "ADM_Roles" ORDER BY "Name" """);

        // Re-run the migration's seed statements exactly as a re-deployment would.
        await ExecuteAsync(connection,
            """
            UPDATE "ADM_Roles"
            SET "Permissions" = (SELECT jsonb_agg(value ORDER BY value) FROM (SELECT jsonb_array_elements_text("Permissions") AS value UNION SELECT 'positions.read') AS merged)
            WHERE "IsSystem" AND NOT "Permissions" ? 'positions.read';
            UPDATE "ADM_Roles"
            SET "Permissions" = (SELECT jsonb_agg(value ORDER BY value) FROM (SELECT jsonb_array_elements_text("Permissions") AS value UNION SELECT 'positions.manage') AS merged)
            WHERE "IsSystem" AND "Name" IN ('rrhh_admin', 'rrhh_user') AND NOT "Permissions" ? 'positions.manage';
            """);

        Assert.Equal(before, await QueryAsync("""SELECT "Name" || '=' || "Permissions"::text FROM "ADM_Roles" ORDER BY "Name" """));
        Assert.Contains("custom_viewer=[\"candidates.read\"]", before);
    }

    [Fact]
    public async Task The_runtime_role_holds_exactly_select_insert_and_update()
    {
        await ResetAsync();

        var privileges = await QueryAsync(
            """
            SELECT privilege_type FROM information_schema.role_table_grants
            WHERE grantee = 'ktl_runtime' AND table_name = @table
            ORDER BY privilege_type
            """);

        Assert.Equal(["INSERT", "SELECT", "UPDATE"], privileges);
    }

    [Theory]
    [InlineData("DELETE FROM \"OPS_Positions\"")]
    [InlineData("TRUNCATE \"OPS_Positions\"")]
    [InlineData("ALTER TABLE \"OPS_Positions\" ADD COLUMN \"Injected\" text")]
    [InlineData("DROP INDEX \"UX_OPS_Positions_NormalizedTitle\"")]
    [InlineData("UPDATE \"AUD_Events\" SET \"OutcomeCode\" = 'rewritten'")]
    public async Task The_runtime_role_is_refused_deletion_ddl_and_audit_rewrites(string statement)
    {
        await ResetAsync();
        await InsertAsync("Permanece");

        await using var connection = await OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await using (var role = new NpgsqlCommand("SET LOCAL ROLE ktl_runtime", connection, transaction))
        {
            await role.ExecuteNonQueryAsync();
        }
        await using var command = new NpgsqlCommand(statement, connection, transaction);
        var refusal = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
        await transaction.RollbackAsync();

        // A missing grant and a missing ownership (ALTER, DROP INDEX) both report 42501.
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, refusal.SqlState);
        Assert.Equal(["Permanece"], await QueryAsync("""SELECT "Title" FROM "OPS_Positions" WHERE @table IS NOT NULL"""));
    }

    [Fact]
    public async Task The_runtime_role_can_read_insert_and_update_positions()
    {
        await ResetAsync();
        var id = await InsertAsync("Operativa");

        await using var connection = await OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await using (var role = new NpgsqlCommand("SET LOCAL ROLE ktl_runtime", connection, transaction))
        {
            await role.ExecuteNonQueryAsync();
        }
        await using (var update = new NpgsqlCommand($"UPDATE \"{Table}\" SET \"Status\" = 'closed' WHERE \"Id\" = @id", connection, transaction))
        {
            update.Parameters.AddWithValue("id", id);
            Assert.Equal(1, await update.ExecuteNonQueryAsync());
        }
        await using (var select = new NpgsqlCommand($"SELECT count(*) FROM \"{Table}\"", connection, transaction))
        {
            Assert.Equal(1L, await select.ExecuteScalarAsync());
        }
        await transaction.RollbackAsync();
    }

    private static Position NewPosition(string title) =>
        new(Guid.CreateVersion7(), title, string.Empty, string.Empty, EmptyRequirements, 1, DateTimeOffset.UtcNow);

    private PositionRepository NewRepository(ApplicationDbContext dbContext) =>
        new(dbContext, new SystemActor(), new FixedCorrelation());

    private async Task<Guid> InsertAsync(string title)
    {
        await using var dbContext = NewDbContext();
        var position = NewPosition(title);
        dbContext.Positions.Add(position);
        await dbContext.SaveChangesAsync();
        return position.Id;
    }

    private async Task ResetAsync()
    {
        await using var dbContext = NewDbContext();
        await dbContext.Database.EnsureDeletedAsync();
        await DatabaseInitializer.MigrateAsync(dbContext, CancellationToken.None);
        // Audited writes need a stored caller behind the actor.
        dbContext.Users.Add(new Domain.Identity.User(ActorUserId, "position-schema-test", "Schema Test", "schema@example.test", "rrhh_admin", DateTimeOffset.UtcNow));
        await dbContext.SaveChangesAsync();
    }

    private static readonly Guid ActorUserId = Guid.CreateVersion7();

    private async Task<NpgsqlConnection> OpenAsync()
    {
        var connection = new NpgsqlConnection(database.ConnectionString);
        await connection.OpenAsync();
        return connection;
    }

    private static async Task ExecuteAsync(NpgsqlConnection connection, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    private async Task<List<string>> QueryAsync(string sql)
    {
        await using var connection = await OpenAsync();
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

    private ApplicationDbContext NewDbContext() =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(database.ConnectionString).Options);

    private sealed class SystemActor : ICurrentActor
    {
        public string? ExternalKey => "position-schema-test";
        public Guid? UserId => ActorUserId;
        public bool IsAuthenticated => true;
        public bool HasPermission(string permission) => true;
    }

    private sealed class FixedCorrelation : ICorrelationContext
    {
        public string CorrelationId => "0123456789abcdef0123456789abcdef";
    }
}
