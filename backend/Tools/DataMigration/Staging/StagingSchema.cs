using KeplerTalento.Tools.DataMigration.Export;
using Npgsql;

namespace KeplerTalento.Tools.DataMigration.Staging;

/// <summary>
/// The export, copied verbatim as text into a schema of its own in the target database.
/// </summary>
/// <remarks>
/// The database rather than the operator's disk, because staging holds the same personal
/// data the export does and the database is already the protected, access-controlled,
/// backed-up place. It is dropped on a successful load; on failure it is retained for
/// diagnosis and the operator is told, so retention is never silent.
/// </remarks>
public sealed class StagingSchema(string connectionString) : IAsyncDisposable
{
    public const string SchemaName = "migration_staging";

    private bool _created;

    public bool Exists => _created;

    public async Task CreateAsync(CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        // A previous failed run may have left one behind; the operator was told to drop it,
        // and starting from a known-empty schema is safer than appending to a stale one.
        await ExecuteAsync(connection, $"DROP SCHEMA IF EXISTS {SchemaName} CASCADE;", cancellationToken);
        await ExecuteAsync(connection, $"CREATE SCHEMA {SchemaName};", cancellationToken);
        _created = true;
    }

    /// <summary>
    /// Copies one export file into staging with every column typed as text. Nothing is
    /// coerced here: coercion is validation's job, and a staging table that refuses a bad
    /// value would lose the row before it could be reported.
    /// </summary>
    public async Task LoadAsync(string file, CsvDocument document, CancellationToken cancellationToken)
    {
        var table = TableNameFor(file);
        var columns = ExportContract.Columns[file];

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        var definition = string.Join(", ", columns.Select(column => $"\"{column}\" text"));
        await ExecuteAsync(
            connection,
            $"CREATE TABLE {SchemaName}.\"{table}\" (\"__line\" integer, {definition});",
            cancellationToken);

        if (document.Rows.Count == 0)
        {
            return;
        }

        var quoted = string.Join(", ", columns.Select(column => $"\"{column}\""));
        await using var writer = await connection.BeginBinaryImportAsync(
            $"COPY {SchemaName}.\"{table}\" (\"__line\", {quoted}) FROM STDIN (FORMAT BINARY)",
            cancellationToken);
        foreach (var row in document.Rows)
        {
            await writer.StartRowAsync(cancellationToken);
            await writer.WriteAsync(row.LineNumber, NpgsqlTypes.NpgsqlDbType.Integer, cancellationToken);
            foreach (var column in columns)
            {
                await writer.WriteAsync(row[column], NpgsqlTypes.NpgsqlDbType.Text, cancellationToken);
            }
        }
        await writer.CompleteAsync(cancellationToken);
    }

    public async Task<int> CountAsync(string file, CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand(
            $"SELECT count(*) FROM {SchemaName}.\"{TableNameFor(file)}\"",
            connection);
        return (int)(long)(await command.ExecuteScalarAsync(cancellationToken))!;
    }

    public async Task DropAsync(CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await ExecuteAsync(connection, $"DROP SCHEMA IF EXISTS {SchemaName} CASCADE;", cancellationToken);
        _created = false;
    }

    public static string TableNameFor(string file) => Path.GetFileNameWithoutExtension(file);

    private static async Task ExecuteAsync(NpgsqlConnection connection, string sql, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <summary>
    /// Staging is never left behind silently. Disposal without an explicit drop means the
    /// run failed, so the caller is responsible for telling the operator it survives.
    /// </summary>
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
