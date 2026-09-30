using System.Text.Json.Nodes;
using KeplerTalento.Application.Abstractions.Encryption;
using KeplerTalento.Application.Import;
using KeplerTalento.Infrastructure.Encryption;
using KeplerTalento.Infrastructure.Persistence;
using KeplerTalento.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace KeplerTalento.Tools.DataMigration.Encryption;

/// <summary>
/// <c>ktl-migrate encryption ...</c>: key generation, backfill, rotation and verification for
/// KTL-33 field encryption. Operator-only, run against a database, never over HTTP.
/// </summary>
/// <remarks>
/// The backfill and report read and write through plain Npgsql rather than EF: they must see the
/// stored value — plaintext before the backfill, an envelope after — not what a converter makes of
/// it. Every row is updated in one statement guarded by its previous values, so a concurrent
/// application write is never overwritten and a re-run changes nothing that is already done.
/// Messages carry table and column names and counts; never a value, a key or the key file's path.
/// </remarks>
public static class EncryptionCommands
{
    public const string Usage = """
        ktl-migrate encryption <command> [options]

        Commands
          generate-keys --out <file>         Write a new key file: one random 256-bit key per purpose.
                                             Refuses to overwrite an existing file.
          add-key <encryption|blindIndex> --keys <file>
                                             Add a new random key for that purpose and make it active.
                                             Older keys stay for reading until retired.
          retire-key <id> --keys <file>      Remove a key that is no longer active. Run report first:
                                             it must show no value under that key.
          backfill --connection <string> [--keys <file>]
                                             Encrypt every plaintext value, re-encrypt values under a
                                             non-active key and recompute e-mail blind indexes. Run as
                                             the RUNTIME role. Idempotent and resumable.
          report --connection <string> [--keys <file>]
                                             Count values per table, column and key, decrypt all of
                                             them, and fail on any plaintext, unknown key or damage.

        --keys defaults to FieldEncryption__KeyFile, then /run/secrets/ktl_field_keys.
        """;

    private const int BatchSize = 500;

    public static async Task<int> RunAsync(string[] args, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        if (args.Length == 0)
        {
            error.WriteLine(Usage);
            return ExitCodesFor.UsageError;
        }
        var options = ParseOptions(args.Skip(1).ToArray(), out var positional);
        var keyPath = options.TryGetValue("--keys", out var keyFile) ? keyFile : ToolFieldEncryption.KeyFilePath;
        var keys = new Lazy<FieldKeySet>(() => FieldKeySet.Load(keyPath));
        var protector = new AesGcmFieldProtector(() => keys.Value);
        var blindIndex = new HmacBlindIndex(() => keys.Value);
        try
        {
            switch (args[0])
            {
                case "generate-keys" when options.TryGetValue("--out", out var target):
                    return GenerateKeys(target, output, error);
                case "add-key" when positional.Count == 1 && options.ContainsKey("--keys"):
                    return AddKey(options["--keys"], positional[0], output, error);
                case "retire-key" when positional.Count == 1 && options.ContainsKey("--keys"):
                    return RetireKey(options["--keys"], positional[0], output, error);
                case "backfill" when options.TryGetValue("--connection", out var connection):
                    return await BackfillAsync(connection, protector, blindIndex, output, cancellationToken);
                case "report" when options.TryGetValue("--connection", out var connection):
                    return await ReportAsync(connection, protector, blindIndex, output, cancellationToken);
                default:
                    error.WriteLine(Usage);
                    return ExitCodesFor.UsageError;
            }
        }
        catch (FieldKeyException exception)
        {
            error.WriteLine($"Key file refused ({exception.Code}).");
            return ExitCodesFor.Failed;
        }
    }

    private static int GenerateKeys(string path, TextWriter output, TextWriter error)
    {
        if (File.Exists(path))
        {
            error.WriteLine("The key file already exists; refusing to overwrite it.");
            return ExitCodesFor.Failed;
        }
        WriteKeyFile(
            path,
            new FieldKeyRing("e1", new Dictionary<string, byte[]> { ["e1"] = FieldKeySet.NewKey() }),
            new FieldKeyRing("b1", new Dictionary<string, byte[]> { ["b1"] = FieldKeySet.NewKey() }));
        output.WriteLine("Key file written. Escrow a copy now; without it the encrypted data cannot be read.");
        return ExitCodesFor.Success;
    }

    private static int AddKey(string path, string purpose, TextWriter output, TextWriter error)
    {
        var keys = FieldKeySet.Load(path);
        switch (purpose)
        {
            case "encryption":
                var encryption = WithNewKey(keys.Encryption, "e");
                WriteKeyFile(path, encryption, keys.BlindIndex);
                output.WriteLine($"Encryption key {encryption.ActiveId} added and active. Restart the API, then run backfill.");
                return ExitCodesFor.Success;
            case "blindIndex":
                var blindIndex = WithNewKey(keys.BlindIndex, "b");
                WriteKeyFile(path, keys.Encryption, blindIndex);
                output.WriteLine($"Blind-index key {blindIndex.ActiveId} added and active. Restart the API, then run backfill.");
                return ExitCodesFor.Success;
            default:
                error.WriteLine("The purpose must be encryption or blindIndex.");
                return ExitCodesFor.UsageError;
        }
    }

    private static int RetireKey(string path, string id, TextWriter output, TextWriter error)
    {
        var keys = FieldKeySet.Load(path);
        FieldKeyRing Without(FieldKeyRing ring) => new(
            ring.ActiveId,
            ring.Keys.Where(pair => pair.Key != id).ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal));
        if (keys.Encryption.ActiveId == id || keys.BlindIndex.ActiveId == id)
        {
            error.WriteLine("The active key cannot be retired.");
            return ExitCodesFor.Failed;
        }
        if (!keys.Encryption.Keys.ContainsKey(id) && !keys.BlindIndex.Keys.ContainsKey(id))
        {
            error.WriteLine("No key has that identifier.");
            return ExitCodesFor.Failed;
        }
        WriteKeyFile(path, Without(keys.Encryption), Without(keys.BlindIndex));
        output.WriteLine($"Key {id} retired.");
        return ExitCodesFor.Success;
    }

    private static FieldKeyRing WithNewKey(FieldKeyRing ring, string prefix)
    {
        var next = 1;
        while (ring.Keys.ContainsKey($"{prefix}{next}"))
        {
            next++;
        }
        var id = $"{prefix}{next}";
        var keys = ring.Keys.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        keys[id] = FieldKeySet.NewKey();
        return new FieldKeyRing(id, keys);
    }

    private static void WriteKeyFile(string path, FieldKeyRing encryption, FieldKeyRing blindIndex)
    {
        // Written beside the target and moved into place, so a crash never leaves half a key file.
        var temporary = path + ".tmp";
        File.WriteAllText(temporary, FieldKeySet.Serialize(encryption, blindIndex));
        File.Move(temporary, path, overwrite: true);
    }

    // ---- backfill -------------------------------------------------------------------------

    public static async Task<int> BackfillAsync(
        string connectionString,
        AesGcmFieldProtector protector,
        IBlindIndex blindIndex,
        TextWriter output,
        CancellationToken cancellationToken)
    {
        var plan = Plan(connectionString);
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        foreach (var (table, columns) in plan.Columns)
        {
            var withHash = table == CandidateConfiguration.Table;
            var selected = withHash ? [.. columns, CandidateConfiguration.EmailHash] : columns;
            var changed = await ForEachBatchAsync(connection, table, selected, async (batch, transaction) =>
            {
                var updates = 0;
                foreach (var row in batch)
                {
                    var next = new Dictionary<string, string?>(StringComparer.Ordinal);
                    foreach (var column in columns)
                    {
                        var value = row.Values[column];
                        if (value is null || protector.IsProtectedWithActiveKey(value))
                        {
                            continue;
                        }
                        var context = FieldContext.For(table, column);
                        var plaintext = AesGcmFieldProtector.IsEnvelope(value) ? protector.Unprotect(value, context) : value;
                        next[column] = protector.Protect(plaintext, context);
                    }
                    if (withHash)
                    {
                        var storedEmail = row.Values[nameof(KeplerTalento.Domain.Candidates.Candidate.Email)]!;
                        var email = AesGcmFieldProtector.IsEnvelope(storedEmail)
                            ? protector.Unprotect(storedEmail, FieldContext.For(table, nameof(KeplerTalento.Domain.Candidates.Candidate.Email)))
                            : storedEmail;
                        var hash = blindIndex.Compute(CandidateImportRowEvaluator.NormalizeEmail(email));
                        if (!string.Equals(row.Values[CandidateConfiguration.EmailHash], hash, StringComparison.Ordinal))
                        {
                            next[CandidateConfiguration.EmailHash] = hash;
                        }
                    }
                    if (next.Count > 0 && await UpdateAsync(connection, transaction, table, row, next, cancellationToken))
                    {
                        updates++;
                    }
                }
                return updates;
            }, cancellationToken);
            output.WriteLine($"{table}: {changed} rows updated.");
        }

        foreach (var (table, column) in FieldEncryptionStartupCheck.FilterDocuments)
        {
            var context = FieldContext.For(table, $"{column}.text");
            var changed = await ForEachBatchAsync(connection, table, [column], async (batch, transaction) =>
            {
                var updates = 0;
                foreach (var row in batch)
                {
                    var document = row.Values[column];
                    var next = document is null ? null : ReEncryptFilterText(document, protector, context);
                    if (next is not null && await UpdateAsync(connection, transaction, table, row, new() { [column] = next }, cancellationToken, jsonb: true))
                    {
                        updates++;
                    }
                }
                return updates;
            }, cancellationToken);
            output.WriteLine($"{table}: {changed} rows updated.");
        }

        output.WriteLine("Backfill complete. Run report, then VACUUM FULL the affected tables as the migration role.");
        return ExitCodesFor.Success;
    }

    /// <summary>The document with its text encrypted under the active key, or null when nothing changes.</summary>
    private static string? ReEncryptFilterText(string document, AesGcmFieldProtector protector, FieldContext context)
    {
        if (JsonNode.Parse(document) is not JsonObject json
            || json[EncryptedFilterDocumentConverter.TextMember] is not JsonValue value
            || !value.TryGetValue<string>(out var text)
            || text.Length == 0
            || protector.IsProtectedWithActiveKey(text))
        {
            return null;
        }
        var plaintext = AesGcmFieldProtector.IsEnvelope(text) ? protector.Unprotect(text, context) : text;
        json[EncryptedFilterDocumentConverter.TextMember] = protector.Protect(plaintext, context);
        return json.ToJsonString();
    }

    // ---- report ---------------------------------------------------------------------------

    public static async Task<int> ReportAsync(
        string connectionString,
        AesGcmFieldProtector protector,
        IBlindIndex blindIndex,
        TextWriter output,
        CancellationToken cancellationToken)
    {
        var keys = protector.Keys;
        var plan = Plan(connectionString);
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        var problems = 0;

        foreach (var (table, columns) in plan.Columns)
        {
            var withHash = table == CandidateConfiguration.Table;
            var selected = withHash ? [.. columns, CandidateConfiguration.EmailHash] : columns;
            var tally = columns.ToDictionary(column => column, _ => new ColumnTally(), StringComparer.Ordinal);
            var hashes = new ColumnTally();
            var rows = 0;
            await ForEachBatchAsync(connection, table, selected, (batch, _) =>
            {
                foreach (var row in batch)
                {
                    rows++;
                    string? email = null;
                    foreach (var column in columns)
                    {
                        var decrypted = Classify(row.Values[column], FieldContext.For(table, column), protector, tally[column]);
                        if (column == nameof(KeplerTalento.Domain.Candidates.Candidate.Email))
                        {
                            email = decrypted;
                        }
                    }
                    if (withHash)
                    {
                        var stored = row.Values[CandidateConfiguration.EmailHash] ?? string.Empty;
                        var keyId = stored.Split('.')[0];
                        if (email is not null && blindIndex.ComputeAll(CandidateImportRowEvaluator.NormalizeEmail(email)).Contains(stored))
                        {
                            hashes.Count(keyId);
                        }
                        else
                        {
                            hashes.Invalid++;
                        }
                    }
                }
                return Task.FromResult(0);
            }, cancellationToken);

            output.WriteLine($"{table}: {rows} rows");
            foreach (var (column, counts) in tally)
            {
                output.WriteLine($"  {column}: {counts.Describe()}");
                problems += counts.Plaintext + counts.Invalid;
            }
            if (withHash)
            {
                output.WriteLine($"  {CandidateConfiguration.EmailHash}: {hashes.Describe()}");
                problems += hashes.Invalid;
            }
        }

        foreach (var (table, column) in FieldEncryptionStartupCheck.FilterDocuments)
        {
            var tally = new ColumnTally();
            var context = FieldContext.For(table, $"{column}.text");
            await ForEachBatchAsync(connection, table, [column], (batch, _) =>
            {
                foreach (var row in batch)
                {
                    var text = row.Values[column] is { } document
                        && JsonNode.Parse(document) is JsonObject json
                        && json[EncryptedFilterDocumentConverter.TextMember] is JsonValue value
                        && value.TryGetValue<string>(out var member)
                        && member.Length > 0
                            ? member
                            : null;
                    Classify(text, context, protector, tally);
                }
                return Task.FromResult(0);
            }, cancellationToken);
            output.WriteLine($"{table}.{column}.text: {tally.Describe()}");
            problems += tally.Plaintext + tally.Invalid;
        }

        await using (var staging = new NpgsqlCommand("SELECT EXISTS (SELECT 1 FROM pg_namespace WHERE nspname = 'migration_staging')", connection))
        {
            if ((bool)(await staging.ExecuteScalarAsync(cancellationToken))!)
            {
                output.WriteLine("migration_staging: PRESENT — it holds plaintext; drop it.");
                problems++;
            }
        }

        output.WriteLine($"Active keys: encryption {keys.Encryption.ActiveId}, blind index {keys.BlindIndex.ActiveId}.");
        output.WriteLine(problems == 0 ? "Report: every value is encrypted and readable." : $"Report: {problems} problem(s).");
        return problems == 0 ? ExitCodesFor.Success : ExitCodesFor.NotReconciled;
    }

    private static string? Classify(string? value, FieldContext context, AesGcmFieldProtector protector, ColumnTally tally)
    {
        if (value is null)
        {
            tally.Nulls++;
            return null;
        }
        if (!AesGcmFieldProtector.IsEnvelope(value))
        {
            tally.Plaintext++;
            return value;
        }
        try
        {
            var plaintext = protector.Unprotect(value, context);
            tally.Count(AesGcmFieldProtector.KeyIdOf(value)!);
            return plaintext;
        }
        catch (FieldDecryptionException)
        {
            tally.Invalid++;
            return null;
        }
    }

    private sealed class ColumnTally
    {
        public int Nulls { get; set; }
        public int Plaintext { get; set; }
        public int Invalid { get; set; }
        public SortedDictionary<string, int> ByKey { get; } = new(StringComparer.Ordinal);

        public void Count(string keyId) => ByKey[keyId] = ByKey.GetValueOrDefault(keyId) + 1;

        public string Describe()
        {
            var keys = string.Join(", ", ByKey.Select(pair => $"{pair.Key}={pair.Value}"));
            return $"keys[{keys}] null={Nulls} plaintext={Plaintext} invalid={Invalid}";
        }
    }

    // ---- shared ---------------------------------------------------------------------------

    private sealed record EncryptionPlan(IReadOnlyList<(string Table, IReadOnlyList<string> Columns)> Columns);

    /// <summary>The encrypted columns, read from the EF model so the tool cannot drift from it.</summary>
    private static EncryptionPlan Plan(string connectionString)
    {
        using var dbContext = new ApplicationDbContext(new Microsoft.EntityFrameworkCore.DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connectionString).Options);
        return new EncryptionPlan([.. FieldEncryptionStartupCheck.EncryptedColumnsByTable(dbContext.Model)]);
    }

    private sealed record StoredRow(Guid Id, IReadOnlyDictionary<string, string?> Values);

    /// <summary>
    /// Walks a table in identifier order, one transaction per batch, so an interrupted run keeps
    /// what it finished and resumes cleanly.
    /// </summary>
    private static async Task<int> ForEachBatchAsync(
        NpgsqlConnection connection,
        string table,
        IReadOnlyList<string> columns,
        Func<IReadOnlyList<StoredRow>, NpgsqlTransaction, Task<int>> handle,
        CancellationToken cancellationToken)
    {
        var total = 0;
        Guid? after = null;
        var select = string.Join(", ", columns.Select(column => $"\"{column}\"::text"));
        while (true)
        {
            var batch = new List<StoredRow>(BatchSize);
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
            await using (var command = new NpgsqlCommand(
                $"SELECT \"Id\", {select} FROM \"{table}\" WHERE (@after::uuid IS NULL OR \"Id\" > @after) ORDER BY \"Id\" LIMIT {BatchSize}",
                connection,
                transaction))
            {
                command.Parameters.AddWithValue("after", (object?)after ?? DBNull.Value);
                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    var values = new Dictionary<string, string?>(StringComparer.Ordinal);
                    for (var index = 0; index < columns.Count; index++)
                    {
                        values[columns[index]] = reader.IsDBNull(index + 1) ? null : reader.GetString(index + 1);
                    }
                    batch.Add(new StoredRow(reader.GetGuid(0), values));
                }
            }
            if (batch.Count == 0)
            {
                await transaction.CommitAsync(cancellationToken);
                return total;
            }
            total += await handle(batch, transaction);
            await transaction.CommitAsync(cancellationToken);
            after = batch[^1].Id;
        }
    }

    /// <summary>
    /// Updates one row, all changed columns at once (the envelope checks judge the whole new row),
    /// only if every column still holds what was read. A concurrent write wins; the next run
    /// picks the row up again.
    /// </summary>
    private static async Task<bool> UpdateAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string table,
        StoredRow row,
        Dictionary<string, string?> next,
        CancellationToken cancellationToken,
        bool jsonb = false)
    {
        var columns = next.Keys.ToList();
        var assignments = string.Join(", ", columns.Select((column, index) => jsonb ? $"\"{column}\" = @n{index}::jsonb" : $"\"{column}\" = @n{index}"));
        var guards = string.Join(" AND ", columns.Select((column, index) => $"\"{column}\"::text IS NOT DISTINCT FROM @o{index}"));
        await using var command = new NpgsqlCommand(
            $"UPDATE \"{table}\" SET {assignments} WHERE \"Id\" = @id AND {guards}",
            connection,
            transaction);
        command.Parameters.AddWithValue("id", row.Id);
        for (var index = 0; index < columns.Count; index++)
        {
            command.Parameters.AddWithValue($"n{index}", (object?)next[columns[index]] ?? DBNull.Value);
            command.Parameters.AddWithValue($"o{index}", (object?)row.Values[columns[index]] ?? DBNull.Value);
        }
        return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
    }

    private static Dictionary<string, string> ParseOptions(string[] args, out List<string> positional)
    {
        var options = new Dictionary<string, string>(StringComparer.Ordinal);
        positional = [];
        for (var index = 0; index < args.Length; index++)
        {
            if (args[index].StartsWith("--", StringComparison.Ordinal) && index + 1 < args.Length)
            {
                options[args[index]] = args[++index];
            }
            else
            {
                positional.Add(args[index]);
            }
        }
        return options;
    }

    private static class ExitCodesFor
    {
        public const int Success = 0;
        public const int UsageError = 2;
        public const int Failed = 3;
        public const int NotReconciled = 4;
    }
}
