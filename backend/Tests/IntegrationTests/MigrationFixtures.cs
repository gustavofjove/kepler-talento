using System.Security.Cryptography;

namespace KeplerTalento.Tests.IntegrationTests;

/// <summary>
/// Locates the synthetic export set and names the sentinel prefix every personal-data field
/// in it carries. Nothing here reads production data: the fixtures are the only source the
/// migration tests ever run against.
/// </summary>
public static class MigrationFixtures
{
    /// <summary>
    /// Every personal-data value in the fixtures starts with this, so one search proves a
    /// report or a log carries none of them.
    /// </summary>
    public const string SentinelPrefix = "SENTINEL";

    public static string Root => Path.Combine(AppContext.BaseDirectory, "Fixtures", "ktl-7");

    public static string ExportDirectory => Path.Combine(Root, "export");

    public static string MappingFile => Path.Combine(Root, "mappings.csv");

    public static string DocumentFile(string name) =>
        Path.Combine(ExportDirectory, "files", name);

    public static IReadOnlyList<string> EntityFiles =>
    [
        "candidates.csv",
        "languages.csv",
        "programs.csv",
        "education.csv",
        "experience.csv",
        "skills.csv",
        "documents.csv",
    ];

    public static async Task<string> Sha256Async(string path, CancellationToken cancellationToken)
    {
        await using var content = File.OpenRead(path);
        var hash = await SHA256.HashDataAsync(content, cancellationToken);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>
    /// Reads a fixture CSV into header plus rows keyed by column name. Deliberately simple:
    /// the fixtures contain no quoted commas, and the migration tool's own reader is the one
    /// under test elsewhere.
    /// </summary>
    public static IReadOnlyList<IReadOnlyDictionary<string, string>> ReadCsv(string fileName)
    {
        var lines = File.ReadAllLines(Path.Combine(ExportDirectory, fileName));
        var header = lines[0].Split(',');
        return lines.Skip(1)
            .Where(line => line.Trim().Length > 0)
            .Select(line =>
            {
                var values = line.Split(',');
                return (IReadOnlyDictionary<string, string>)header
                    .Select((name, index) => (name, value: index < values.Length ? values[index] : string.Empty))
                    .ToDictionary(pair => pair.name, pair => pair.value, StringComparer.Ordinal);
            })
            .ToList();
    }
}
