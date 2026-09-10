using Testcontainers.PostgreSql;
using Npgsql;
using Xunit;

namespace KeplerTalento.Tests.IntegrationTests;

public sealed class PostgreSqlFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17.6-alpine")
        .WithDatabase("kepler_talento_test")
        .WithUsername("postgres")
        .WithPassword("test-only-password")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'ktl_runtime') THEN CREATE ROLE ktl_runtime NOLOGIN; END IF; END $$;",
            connection);
        await command.ExecuteNonQueryAsync();
    }
    /// <summary>
    /// Runs a command inside the database container. Used by the rollback drill, which has
    /// to exercise a real <c>pg_dump</c> and restore rather than simulating one.
    /// </summary>
    public async Task<(long ExitCode, string Stdout, string Stderr)> ExecAsync(params string[] command)
    {
        var result = await _container.ExecAsync(command);
        return (result.ExitCode ?? -1, result.Stdout, result.Stderr);
    }

    public const string DatabaseName = "kepler_talento_test";

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();
}
