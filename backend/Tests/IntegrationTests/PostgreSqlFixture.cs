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
    public Task DisposeAsync() => _container.DisposeAsync().AsTask();
}
