using System.Text;
using KeplerTalento.Application.Abstractions.Correlation;
using KeplerTalento.Application.Abstractions.Identity;
using KeplerTalento.Application.Features.Search;
using KeplerTalento.Domain.Candidates;
using KeplerTalento.Domain.Positions;
using KeplerTalento.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;
using Xunit;

namespace KeplerTalento.Tests.IntegrationTests;

/// <summary>
/// Database-level evidence for <c>OPS_PositionCandidates</c> (KTL-30): its constraints and
/// indexes, non-cascading references, the scoped DELETE grant, and list plans that use the
/// indexes without reading position requirements or descriptions.
/// </summary>
public sealed class PositionCandidateSchemaTests(PostgreSqlFixture database) : IClassFixture<PostgreSqlFixture>
{
    private const string Table = "OPS_PositionCandidates";

    [Fact]
    public async Task The_link_table_carries_its_constraints_references_and_indexes()
    {
        await ResetAsync();

        Assert.Equal(
            ["CK_OPS_PositionCandidates_Stage", "CK_OPS_PositionCandidates_Timestamps"],
            await QueryAsync("SELECT conname FROM pg_constraint WHERE conrelid = format('%I', @table)::regclass AND contype = 'c' ORDER BY conname"));
        Assert.Equal(
            ["IX_OPS_PositionCandidates_CandidateId", "PK_OPS_PositionCandidates", "UX_OPS_PositionCandidates_PositionId_CandidateId"],
            await QueryAsync("SELECT indexname FROM pg_indexes WHERE schemaname = 'public' AND tablename = @table ORDER BY indexname"));
        // confdeltype 'r' = RESTRICT: deleting a referenced position or candidate never cascades.
        Assert.Equal(
            ["FK_OPS_PositionCandidates_CND_Candidates_CandidateId=r", "FK_OPS_PositionCandidates_OPS_Positions_PositionId=r"],
            await QueryAsync("SELECT conname || '=' || confdeltype::text FROM pg_constraint WHERE conrelid = format('%I', @table)::regclass AND contype = 'f' ORDER BY conname"));
    }

    [Theory]
    [InlineData("\"Stage\" = 'referred'", "CK_OPS_PositionCandidates_Stage")]
    [InlineData("\"UpdatedAtUtc\" = \"AddedAtUtc\" - interval '1 day'", "CK_OPS_PositionCandidates_Timestamps")]
    public async Task The_table_refuses_rows_the_application_would_never_write(string assignment, string constraint)
    {
        await ResetAsync();
        var (_, _, link) = await SeedLinkAsync();

        await using var connection = await OpenAsync();
        await using var command = new NpgsqlCommand($"UPDATE \"{Table}\" SET {assignment} WHERE \"Id\" = @id", connection);
        command.Parameters.AddWithValue("id", link);
        var failure = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());

        Assert.Equal(PostgresErrorCodes.CheckViolation, failure.SqlState);
        Assert.Equal(constraint, failure.ConstraintName);
    }

    [Fact]
    public async Task A_duplicate_pair_is_refused_and_references_restrict_privileged_deletes()
    {
        await ResetAsync();
        var (position, candidate, _) = await SeedLinkAsync();

        await using var connection = await OpenAsync();
        await using (var duplicate = new NpgsqlCommand(
            $"INSERT INTO \"{Table}\" (\"Id\", \"PositionId\", \"CandidateId\", \"Stage\", \"AddedAtUtc\", \"UpdatedAtUtc\") VALUES (@id, @position, @candidate, 'new', now(), now())", connection))
        {
            duplicate.Parameters.AddWithValue("id", Guid.CreateVersion7());
            duplicate.Parameters.AddWithValue("position", position);
            duplicate.Parameters.AddWithValue("candidate", candidate);
            Assert.Equal(PostgresErrorCodes.UniqueViolation, (await Assert.ThrowsAsync<PostgresException>(() => duplicate.ExecuteNonQueryAsync())).SqlState);
        }
        foreach (var (table, id) in new[] { ("OPS_Positions", position), ("CND_Candidates", candidate) })
        {
            await using var delete = new NpgsqlCommand($"DELETE FROM \"{table}\" WHERE \"Id\" = @id", connection);
            delete.Parameters.AddWithValue("id", id);
            Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, (await Assert.ThrowsAsync<PostgresException>(() => delete.ExecuteNonQueryAsync())).SqlState);
        }
    }

    [Fact]
    public async Task The_runtime_role_holds_delete_on_links_only()
    {
        await ResetAsync();

        Assert.Equal(["DELETE", "INSERT", "SELECT", "UPDATE"], await QueryAsync(
            "SELECT privilege_type FROM information_schema.role_table_grants WHERE grantee = 'ktl_runtime' AND table_name = @table ORDER BY privilege_type"));
        Assert.Equal(["CND_Candidates=false", "OPS_PositionCandidates=true", "OPS_Positions=false"], (await QueryAsync(
            """
            SELECT t || '=' || has_table_privilege('ktl_runtime', format('%I', t), 'DELETE')
            FROM unnest(ARRAY['OPS_PositionCandidates', 'OPS_Positions', 'CND_Candidates']) AS t
            WHERE @table IS NOT NULL
            """)).Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task The_runtime_role_deletes_a_link_but_cannot_truncate_links_or_delete_positions_and_candidates()
    {
        await ResetAsync();
        var (position, candidate, link) = await SeedLinkAsync();

        await AsRuntimeAsync(async (connection, transaction) =>
        {
            await using var delete = new NpgsqlCommand($"DELETE FROM \"{Table}\" WHERE \"Id\" = @id", connection, transaction);
            delete.Parameters.AddWithValue("id", link);
            Assert.Equal(1, await delete.ExecuteNonQueryAsync());
        });

        foreach (var statement in new[] { $"TRUNCATE \"{Table}\"", $"DELETE FROM \"OPS_Positions\" WHERE \"Id\" = '{position}'", $"DELETE FROM \"CND_Candidates\" WHERE \"Id\" = '{candidate}'" })
        {
            await AsRuntimeAsync(async (connection, transaction) =>
            {
                await using var command = new NpgsqlCommand(statement, connection, transaction);
                Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, (await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync())).SqlState);
            });
        }
    }

    [Fact]
    public async Task Link_lists_use_their_indexes_and_never_read_requirements_or_descriptions()
    {
        await ResetAsync();
        var (position, candidate) = await SeedVolumeAsync();

        var capture = new CommandCapture();
        await using (var dbContext = NewDbContext(capture))
        {
            var repository = new PositionRepository(dbContext, new NoActor(), new NoCorrelation());
            await repository.ListCandidatesAsync(position, CancellationToken.None);
            await repository.ListForCandidateAsync(candidate, CancellationToken.None);
        }

        Assert.Equal(2, capture.Commands.Count);
        await using var connection = await OpenAsync();
        await using (var off = new NpgsqlCommand("SET enable_seqscan = off", connection)) await off.ExecuteNonQueryAsync();
        var plans = new List<string>();
        foreach (var (sql, parameters) in capture.Commands)
        {
            await using var explain = new NpgsqlCommand($"EXPLAIN (VERBOSE) {sql}", connection);
            foreach (var (name, value) in parameters) explain.Parameters.AddWithValue(name, value ?? DBNull.Value);
            await using var reader = await explain.ExecuteReaderAsync();
            var plan = new StringBuilder();
            while (await reader.ReadAsync()) plan.AppendLine(reader.GetString(0));
            plans.Add(plan.ToString());
        }

        Assert.Contains("UX_OPS_PositionCandidates_PositionId_CandidateId", plans[0], StringComparison.Ordinal);
        Assert.Contains("IX_OPS_PositionCandidates_CandidateId", plans[1], StringComparison.Ordinal);
        foreach (var projected in plans.SelectMany(ProjectedOutputs))
        {
            Assert.DoesNotContain("\"Requirements\"", projected, StringComparison.Ordinal);
            Assert.DoesNotContain("\"Description\"", projected, StringComparison.Ordinal);
            Assert.DoesNotContain("\"Notes\"", projected, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// The output lists of every node above the scans, which is what the query passes upward. A
    /// scan may print PostgreSQL's physical tlist (every column of the row) as an optimization
    /// that copies no value and never detoasts one, so scan outputs prove nothing either way.
    /// </summary>
    private static IEnumerable<string> ProjectedOutputs(string plan)
    {
        var lines = plan.Split('\n');
        for (var index = 1; index < lines.Length; index++)
        {
            if (lines[index].TrimStart().StartsWith("Output:", StringComparison.Ordinal)
                && !lines[index - 1].Contains(" Scan ", StringComparison.Ordinal))
            {
                yield return lines[index];
            }
        }
    }

    private async Task<(Guid Position, Guid Candidate)> SeedVolumeAsync()
    {
        await using var dbContext = NewDbContext();
        var requirements = SearchFilterDocument.Serialize(SearchFilterNormalization.Normalize(null, "Requirements"));
        var now = DateTimeOffset.UtcNow;
        var positions = Enumerable.Range(0, 50).Select(index => new Position(Guid.CreateVersion7(), $"Posición {index}", "<p>Texto</p>", "Madrid", requirements, 1, now)).ToList();
        var candidates = Enumerable.Range(0, 200).Select(index => new Candidate(Guid.CreateVersion7(), $"Nombre{index}", "Apellido", now)).ToList();
        dbContext.Positions.AddRange(positions);
        dbContext.Candidates.AddRange(candidates);
        foreach (var (position, index) in positions.Select((p, i) => (p, i)))
            foreach (var candidate in candidates.Skip(index % 10 * 20).Take(20))
                dbContext.PositionCandidates.Add(new PositionCandidate(Guid.CreateVersion7(), position.Id, candidate.Id, now));
        await dbContext.SaveChangesAsync();
        await dbContext.Database.ExecuteSqlRawAsync("ANALYZE \"OPS_PositionCandidates\"; ANALYZE \"OPS_Positions\"; ANALYZE \"CND_Candidates\";");
        return (positions[0].Id, candidates[0].Id);
    }

    private async Task<(Guid Position, Guid Candidate, Guid Link)> SeedLinkAsync()
    {
        await using var dbContext = NewDbContext();
        var now = DateTimeOffset.UtcNow;
        var position = new Position(Guid.CreateVersion7(), "Enlazada", string.Empty, string.Empty,
            SearchFilterDocument.Serialize(SearchFilterNormalization.Normalize(null, "Requirements")), 1, now);
        var candidate = new Candidate(Guid.CreateVersion7(), "Ana", "Enlazada", now);
        var link = new PositionCandidate(Guid.CreateVersion7(), position.Id, candidate.Id, now);
        dbContext.Positions.Add(position);
        dbContext.Candidates.Add(candidate);
        dbContext.PositionCandidates.Add(link);
        await dbContext.SaveChangesAsync();
        return (position.Id, candidate.Id, link.Id);
    }

    private async Task AsRuntimeAsync(Func<NpgsqlConnection, NpgsqlTransaction, Task> action)
    {
        await using var connection = await OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await using (var role = new NpgsqlCommand("SET LOCAL ROLE ktl_runtime", connection, transaction)) await role.ExecuteNonQueryAsync();
        await action(connection, transaction);
        await transaction.RollbackAsync();
    }

    private async Task ResetAsync()
    {
        await using var dbContext = NewDbContext();
        await dbContext.Database.EnsureDeletedAsync();
        await DatabaseInitializer.MigrateAsync(dbContext, CancellationToken.None);
    }

    private async Task<NpgsqlConnection> OpenAsync()
    {
        var connection = new NpgsqlConnection(database.ConnectionString);
        await connection.OpenAsync();
        return connection;
    }

    private async Task<List<string>> QueryAsync(string sql)
    {
        await using var connection = await OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("table", Table);
        await using var reader = await command.ExecuteReaderAsync();
        var rows = new List<string>();
        while (await reader.ReadAsync()) rows.Add(reader.GetString(0));
        return rows;
    }

    private ApplicationDbContext NewDbContext() =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(database.ConnectionString).Options);

    private ApplicationDbContext NewDbContext(CommandCapture capture) =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(database.ConnectionString).AddInterceptors(capture).Options);

    /// <summary>Records every reader command EF sends, so EXPLAIN runs on the exact statements.</summary>
    private sealed class CommandCapture : DbCommandInterceptor
    {
        public List<(string Sql, List<(string Name, object? Value)> Parameters)> Commands { get; } = [];

        public override ValueTask<InterceptionResult<System.Data.Common.DbDataReader>> ReaderExecutingAsync(
            System.Data.Common.DbCommand command, CommandEventData eventData, InterceptionResult<System.Data.Common.DbDataReader> result, CancellationToken cancellationToken = default)
        {
            Commands.Add((command.CommandText, [.. command.Parameters.Cast<System.Data.Common.DbParameter>().Select(p => (p.ParameterName, (object?)p.Value))]));
            return ValueTask.FromResult(result);
        }
    }

    private sealed class NoActor : ICurrentActor
    {
        public string? ExternalKey => null;
        public Guid? UserId => null;
        public bool IsAuthenticated => false;
        public bool HasPermission(string permission) => false;
    }

    private sealed class NoCorrelation : ICorrelationContext
    {
        public string CorrelationId => string.Empty;
    }
}
