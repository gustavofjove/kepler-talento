using System.Runtime.CompilerServices;
using KeplerTalento.Infrastructure.Encryption;
using Microsoft.EntityFrameworkCore;

namespace KeplerTalento.Tests.IntegrationTests;

/// <summary>
/// One key set for the whole test run (KTL-33), shared by the API hosts the tests start and the
/// contexts they open directly, so each can read what the other wrote.
/// </summary>
/// <remarks>
/// The keys are generated per run and written to a temporary file; nothing is committed. The
/// module initializer points every host at that file through the same environment variable an
/// operator would use, so the host goes through its real key loading and startup checks.
/// </remarks>
public static class TestFieldEncryption
{
    public static readonly string KeyFile = Path.Combine(Path.GetTempPath(), $"ktl-test-field-keys-{Environment.ProcessId}.json");

    private static readonly Lazy<FieldKeySet> Keys = new(() => FieldKeySet.Load(KeyFile));

    public static AesGcmFieldProtector Protector { get; } = new(() => Keys.Value);

    public static HmacBlindIndex BlindIndex { get; } = new(() => Keys.Value);

    [ModuleInitializer]
    internal static void Initialize()
    {
        if (!File.Exists(KeyFile))
        {
            File.WriteAllText(KeyFile, FieldKeySet.Serialize(
                new FieldKeyRing("test-e1", new Dictionary<string, byte[]> { ["test-e1"] = FieldKeySet.NewKey() }),
                new FieldKeyRing("test-b1", new Dictionary<string, byte[]> { ["test-b1"] = FieldKeySet.NewKey() })));
        }
        Environment.SetEnvironmentVariable("FieldEncryption__KeyFile", KeyFile);
        AppDomain.CurrentDomain.ProcessExit += (_, _) =>
        {
            try
            {
                File.Delete(KeyFile);
            }
            catch (IOException)
            {
            }
        };
    }

    /// <summary>Opens a context the way the API does, with the run's keys.</summary>
    public static DbContextOptionsBuilder<TContext> UseTestFieldEncryption<TContext>(this DbContextOptionsBuilder<TContext> builder)
        where TContext : DbContext =>
        builder.UseFieldEncryption(Protector, BlindIndex);
}
