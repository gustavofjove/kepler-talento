using KeplerTalento.Infrastructure.Persistence;
using KeplerTalento.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace KeplerTalento.Infrastructure.Encryption;

/// <summary>
/// The API refuses to serve when encryption cannot be trusted (KTL-33 design decision 4).
/// </summary>
/// <remarks>
/// Runs on the serving path only, after the <c>--migrate</c> branch, so the migrator never needs
/// the keys. It fails when the keys are missing or malformed, when any encrypted column still
/// holds a value that is not an envelope (the backfill has not finished), and when the legacy
/// migration staging schema — plaintext by construction — has been left behind. Every failure
/// carries a stable code and at most a table name; never a value, a path or a key.
/// </remarks>
public static class FieldEncryptionStartupCheck
{
    public const string PlaintextPresent = "encryption.plaintext.present";
    public const string StagingPresent = "encryption.staging.present";

    /// <summary>The filter documents whose <c>text</c> member is encrypted (design decision 7).</summary>
    public static readonly (string Table, string Column)[] FilterDocuments =
    [
        (SearchPresetConfiguration.Table, "Filters"),
        (PositionConfiguration.Table, "Requirements"),
    ];

    public static async Task RunAsync(AesGcmFieldProtector protector, ApplicationDbContext dbContext, CancellationToken cancellationToken)
    {
        // Loading the key set validates it; a FieldKeyException already carries its stable code.
        _ = protector.Keys;

        foreach (var (table, columns) in EncryptedColumnsByTable(dbContext.Model))
        {
            var predicate = string.Join(
                " OR ",
                columns.Select(column => $"(\"{column}\" IS NOT NULL AND \"{column}\" NOT LIKE 'ktl1.%')"));
            if (table == CandidateConfiguration.Table)
            {
                predicate += $" OR \"{CandidateConfiguration.EmailHash}\" = ''";
            }
            if (await ExistsAsync(dbContext, $"SELECT EXISTS (SELECT 1 FROM \"{table}\" WHERE {predicate}) AS \"Value\"", cancellationToken))
            {
                throw new FieldEncryptionStartupException(PlaintextPresent, table);
            }
        }

        foreach (var (table, column) in FilterDocuments)
        {
            var sql = $"""
                SELECT EXISTS (
                    SELECT 1 FROM "{table}"
                    WHERE coalesce("{column}"->>'text', '') <> '' AND "{column}"->>'text' NOT LIKE 'ktl1.%'
                ) AS "Value"
                """;
            if (await ExistsAsync(dbContext, sql, cancellationToken))
            {
                throw new FieldEncryptionStartupException(PlaintextPresent, table);
            }
        }

        if (await ExistsAsync(dbContext, "SELECT EXISTS (SELECT 1 FROM pg_namespace WHERE nspname = 'migration_staging') AS \"Value\"", cancellationToken))
        {
            throw new FieldEncryptionStartupException(StagingPresent, "migration_staging");
        }
    }

    public static IEnumerable<(string Table, IReadOnlyList<string> Columns)> EncryptedColumnsByTable(IModel model) =>
        FieldEncryptionModel.EncryptedProperties(model)
            .GroupBy(property => ((IReadOnlyEntityType)property.DeclaringType).GetTableName()!, StringComparer.Ordinal)
            .Select(group => (group.Key, (IReadOnlyList<string>)[.. group.Select(property => property.Name)]));

    private static async Task<bool> ExistsAsync(ApplicationDbContext dbContext, string sql, CancellationToken cancellationToken) =>
        // Table and column names come from the EF model and the literal list above, never from input.
#pragma warning disable EF1002
        await dbContext.Database.SqlQueryRaw<bool>(sql).SingleAsync(cancellationToken);
#pragma warning restore EF1002
}

/// <summary>A startup refusal. Safe to log: a stable code and a table name, nothing else.</summary>
public sealed class FieldEncryptionStartupException(string code, string table)
    : Exception($"Field encryption check failed ({code}) for {table}.")
{
    public string Code { get; } = code;

    public string Table { get; } = table;
}
