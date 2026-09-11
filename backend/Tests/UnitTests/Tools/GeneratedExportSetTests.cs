using System.Globalization;
using KeplerTalento.Domain.Catalogs;
using KeplerTalento.Infrastructure.Persistence;
using KeplerTalento.Tools.DataMigration.Export;
using KeplerTalento.Tools.DataMigration.Resolution;
using KeplerTalento.Tools.DataMigration.Validation;
using KeplerTalento.Tools.TestDataGenerator;
using Xunit;

namespace KeplerTalento.Tests.UnitTests.Tools;

/// <summary>
/// The generator's contract with ktl-migrate: a generated set loads whole. Both halves are
/// checked without a database, because both are pure — <see cref="RowValidator"/> reads only
/// the CSV, and <see cref="CatalogResolver"/> only the vocabulary it is constructed with.
/// A failure here means a developer's test data would have arrived short of rows.
/// </summary>
public sealed class GeneratedExportSetTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"ktl-testdata-{Guid.NewGuid():N}");

    /// <summary>
    /// Which catalog family each reference column is resolved against, mirroring the
    /// loader's own table.
    /// </summary>
    private static readonly (string File, string Column, string Family)[] References =
    [
        (ExportContract.Languages, "Language", CatalogFamilies.Language),
        (ExportContract.Languages, "Level", CatalogFamilies.LanguageLevel),
        (ExportContract.Programs, "Program", CatalogFamilies.Program),
        (ExportContract.Programs, "Level", CatalogFamilies.ProgramLevel),
        (ExportContract.Skills, "Skill", CatalogFamilies.Skill),
        (ExportContract.Skills, "Level", CatalogFamilies.SkillLevel),
        (ExportContract.Education, "EducationType", CatalogFamilies.EducationType),
        (ExportContract.Education, "Status", CatalogFamilies.EducationStatus),
        (ExportContract.Experience, "Sector", CatalogFamilies.Sector),
    ];

    /// <summary>
    /// A fixed anchor, so these tests assert the same thing on every day they run. The
    /// generator reads no clock; the anchor is the only date input it has.
    /// </summary>
    private static readonly DateOnly Anchor = new(2026, 9, 11);

    private async Task<ExportSet> GenerateAsync(
        int candidates = 60,
        int seed = 1,
        DateOnly? asOf = null)
    {
        var options = new GeneratorOptions
        {
            OutputDirectory = _root,
            Candidates = candidates,
            Seed = seed,
            AsOf = asOf ?? Anchor,
        };
        await new ExportSetGenerator(options).WriteAsync(CancellationToken.None);

        var read = new ExportReader().TryRead(
            Path.Combine(_root, "export"), out var exportSet, out var problems);
        Assert.True(read, string.Join("; ", problems.Select(problem => problem.ToString())));
        return exportSet;
    }

    [Fact]
    public async Task A_generated_set_has_no_row_level_problems()
    {
        var exportSet = await GenerateAsync();

        var problems = new RowValidator().Validate(exportSet);

        Assert.Empty(problems);
    }

    [Fact]
    public async Task Every_generated_reference_resolves_against_the_seeded_vocabulary()
    {
        var exportSet = await GenerateAsync();
        var resolver = SeededResolver();

        foreach (var (file, column, family) in References)
        {
            foreach (var row in exportSet[file].Rows)
            {
                Assert.True(
                    resolver.Resolve(family, row[column]).Resolved,
                    $"{file}:{row.LineNumber} column '{column}' did not resolve in family '{family}'.");
            }
        }
        Assert.Empty(resolver.UnresolvedValues);
    }

    [Fact]
    public async Task The_same_seed_and_anchor_reproduce_the_same_set()
    {
        var first = await File.ReadAllTextAsync(
            await GenerateCandidatesFileAsync(seed: 7, asOf: Anchor), CancellationToken.None);
        Directory.Delete(_root, recursive: true);
        var second = await File.ReadAllTextAsync(
            await GenerateCandidatesFileAsync(seed: 7, asOf: Anchor), CancellationToken.None);

        Assert.Equal(first, second);
    }

    /// <summary>
    /// The regression guard for the anchor itself. If the generator ever reads the clock
    /// again instead of <see cref="GeneratorOptions.AsOf"/>, these two runs produce identical
    /// files and this fails — where the reproducibility test above would keep passing, since
    /// both of its runs happen on the same day.
    /// </summary>
    [Fact]
    public async Task A_different_anchor_moves_the_generated_dates()
    {
        var earlier = await File.ReadAllTextAsync(
            await GenerateCandidatesFileAsync(seed: 7, asOf: new DateOnly(2024, 1, 15)),
            CancellationToken.None);
        Directory.Delete(_root, recursive: true);
        var later = await File.ReadAllTextAsync(
            await GenerateCandidatesFileAsync(seed: 7, asOf: new DateOnly(2026, 9, 11)),
            CancellationToken.None);

        Assert.NotEqual(earlier, later);
    }

    [Fact]
    public async Task No_generated_date_falls_after_the_anchor()
    {
        var asOf = new DateOnly(2024, 6, 30);
        var exportSet = await GenerateAsync(asOf: asOf);

        foreach (var row in exportSet[ExportContract.Candidates].Rows)
        {
            foreach (var column in new[] { "ReceivedAt", "ConsentAt", "DeletedAt" })
            {
                if (row.IsEmpty(column))
                {
                    continue;
                }
                Assert.True(
                    DateOnly.Parse(row[column], CultureInfo.InvariantCulture) <= asOf,
                    $"{column} on {row[ExportContract.SourceKeyColumn]} falls after the anchor.");
            }
        }
    }

    [Fact]
    public async Task Every_generated_document_hash_matches_the_file_it_names()
    {
        var exportSet = await GenerateAsync();
        var rows = exportSet[ExportContract.Documents].Rows;
        Assert.NotEmpty(rows);

        foreach (var row in rows)
        {
            var path = Path.Combine(exportSet.FilesDirectory, row["RelativePath"]);
            Assert.True(File.Exists(path), $"{row["RelativePath"]} is named in the manifest but absent.");

            var actual = Convert.ToHexStringLower(
                System.Security.Cryptography.SHA256.HashData(
                    await File.ReadAllBytesAsync(path, CancellationToken.None)));
            Assert.Equal(row["Sha256"], actual);
        }
    }

    private async Task<string> GenerateCandidatesFileAsync(int seed, DateOnly asOf)
    {
        await GenerateAsync(candidates: 20, seed: seed, asOf: asOf);
        return Path.Combine(_root, "export", ExportContract.Candidates);
    }

    private static CatalogResolver SeededResolver() =>
        new(CatalogSeedData.Families.SelectMany(family => family.Value.Select(value =>
            new CatalogResolver.CatalogEntry(
                Guid.CreateVersion7(), family.Key, value.ResolveCode(), value.NameEs))));

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
