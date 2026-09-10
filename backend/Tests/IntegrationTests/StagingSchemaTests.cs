using KeplerTalento.Tools.DataMigration.Export;
using KeplerTalento.Tools.DataMigration.Staging;
using KeplerTalento.Tools.DataMigration.Validation;
using Npgsql;
using Xunit;

namespace KeplerTalento.Tests.IntegrationTests;

public sealed class StagingSchemaTests(PostgreSqlFixture database) : IClassFixture<PostgreSqlFixture>
{
    private async Task<bool> SchemaExistsAsync()
    {
        await using var connection = new NpgsqlConnection(database.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT count(*) FROM information_schema.schemata WHERE schema_name = @name",
            connection);
        command.Parameters.AddWithValue("name", StagingSchema.SchemaName);
        return (long)(await command.ExecuteScalarAsync())! > 0;
    }

    private static ExportSet ReadFixtures()
    {
        Assert.True(
            new ExportReader().TryRead(MigrationFixtures.ExportDirectory, out var exportSet, out var problems),
            string.Join("; ", problems));
        return exportSet;
    }

    [Fact]
    public async Task The_export_is_copied_verbatim_into_a_schema_of_its_own_and_dropped_after()
    {
        var exportSet = ReadFixtures();
        var staging = new StagingSchema(database.ConnectionString);
        await staging.CreateAsync(CancellationToken.None);
        try
        {
            Assert.True(await SchemaExistsAsync());
            foreach (var file in ExportContract.Files)
            {
                await staging.LoadAsync(file, exportSet[file], CancellationToken.None);
                Assert.Equal(exportSet.RowCount(file), await staging.CountAsync(file, CancellationToken.None));
            }

            // Verbatim means verbatim: a value that validation will later reject is still
            // present in staging, because a row lost on the way in cannot be reported.
            await using var connection = new NpgsqlConnection(database.ConnectionString);
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand(
                $"SELECT \"ConsentAt\" FROM {StagingSchema.SchemaName}.\"candidates\" WHERE \"SourceKey\" = 'C-003'",
                connection);
            Assert.Equal(string.Empty, (string)(await command.ExecuteScalarAsync())!);
        }
        finally
        {
            await staging.DropAsync(CancellationToken.None);
        }

        Assert.False(await SchemaExistsAsync());
    }

    [Fact]
    public async Task Creating_staging_clears_a_schema_a_previous_failed_run_left_behind()
    {
        var exportSet = ReadFixtures();
        var staging = new StagingSchema(database.ConnectionString);
        await staging.CreateAsync(CancellationToken.None);
        await staging.LoadAsync(ExportContract.Candidates, exportSet[ExportContract.Candidates], CancellationToken.None);
        Assert.Equal(7, await staging.CountAsync(ExportContract.Candidates, CancellationToken.None));

        // Simulates the next run after a failure, without the operator having dropped it.
        await staging.CreateAsync(CancellationToken.None);
        await staging.LoadAsync(ExportContract.Candidates, exportSet[ExportContract.Candidates], CancellationToken.None);

        Assert.Equal(7, await staging.CountAsync(ExportContract.Candidates, CancellationToken.None));
        await staging.DropAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Dropping_staging_that_does_not_exist_is_not_an_error()
    {
        var staging = new StagingSchema(database.ConnectionString);
        await staging.DropAsync(CancellationToken.None);
        await staging.DropAsync(CancellationToken.None);
        Assert.False(await SchemaExistsAsync());
    }

    [Fact]
    public void The_fixture_export_produces_exactly_the_problems_its_readme_documents()
    {
        var problems = new RowValidator().Validate(ReadFixtures());
        var byCandidate = problems
            .Where(problem => problem.Entity == MigrationEntities.Candidate)
            .ToDictionary(problem => problem.SourceKey, problem => problem.ReasonCode, StringComparer.Ordinal);

        Assert.Equal(ReasonCodes.ConsentMissing, byCandidate["C-003"]);
        Assert.Equal(ReasonCodes.EmailInvalid, byCandidate["C-004"]);
        // C-005 is rejected by the resolver, not here: its language resolves to nothing.
        Assert.Equal(2, byCandidate.Count);

        // C-002 is logically removed in the source and must survive validation, so that it
        // can arrive as logical state rather than as an absence.
        Assert.DoesNotContain("C-002", byCandidate.Keys);
    }

    [Fact]
    public void No_validation_problem_from_the_fixtures_carries_a_sentinel_value()
    {
        var problems = new RowValidator().Validate(ReadFixtures());

        Assert.NotEmpty(problems);
        Assert.All(
            problems,
            problem => Assert.DoesNotContain(
                MigrationFixtures.SentinelPrefix,
                problem.ToString(),
                StringComparison.Ordinal));
    }
}
