using KeplerTalento.Infrastructure.Encryption;
using KeplerTalento.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace KeplerTalento.Tools.DataMigration;

/// <summary>
/// The tool's field encryption keys (KTL-33), read from the same <c>FieldEncryption__KeyFile</c>
/// variable the API uses, defaulting to the same mounted secret. Loaded on first use, so verbs
/// that never touch candidate data do not need them.
/// </summary>
public static class ToolFieldEncryption
{
    private static readonly Lazy<FieldKeySet> Keys = new(() => FieldKeySet.Load(KeyFilePath));

    public static string KeyFilePath =>
        Environment.GetEnvironmentVariable("FieldEncryption__KeyFile") is { Length: > 0 } path
            ? path
            : FieldEncryptionOptions.DefaultKeyFile;

    public static AesGcmFieldProtector Protector { get; } = new(() => Keys.Value);

    public static HmacBlindIndex BlindIndex { get; } = new(() => Keys.Value);

    public static DbContextOptions<ApplicationDbContext> ContextOptions(string connectionString) =>
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(connectionString)
            .UseFieldEncryption(Protector, BlindIndex)
            .Options;
}
