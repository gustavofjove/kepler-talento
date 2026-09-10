using KeplerTalento.Domain.Catalogs;
using KeplerTalento.Infrastructure.Persistence;
using KeplerTalento.Tools.DataMigration.Export;
using KeplerTalento.Tools.DataMigration.Resolution;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace KeplerTalento.Tests.IntegrationTests;

/// <summary>
/// The resolver against the real seeded vocabulary and the real fixture export, rather than
/// a hand-built catalog. This is what proves the three steps line up with the values Access
/// actually holds.
/// </summary>
public sealed class CatalogResolutionTests(PostgreSqlFixture database) : IClassFixture<PostgreSqlFixture>
{
    private async Task<CatalogResolver> ResolverAsync(bool withMappings)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(database.ConnectionString)
            .Options;
        await using var dbContext = new ApplicationDbContext(options);
        await DatabaseInitializer.MigrateAsync(dbContext, CancellationToken.None);
        await DatabaseInitializer.SeedCatalogsAsync(dbContext, CancellationToken.None);
        var entries = await dbContext.CatalogItems
            .AsNoTracking()
            .Select(item => new CatalogResolver.CatalogEntry(item.Id, item.Family, item.Code, item.NameEs))
            .ToListAsync();

        IReadOnlyDictionary<(string, string), string>? mappings = null;
        if (withMappings)
        {
            Assert.True(MappingFile.TryRead(MigrationFixtures.MappingFile, out var read, out var problems),
                string.Join("; ", problems));
            mappings = read;
        }
        return new CatalogResolver(entries, mappings);
    }

    private static IEnumerable<CsvRow> LanguageRows()
    {
        Assert.True(new ExportReader().TryRead(MigrationFixtures.ExportDirectory, out var exportSet, out _));
        return exportSet[ExportContract.Languages].Rows;
    }

    [Fact]
    public async Task The_seeded_vocabulary_resolves_the_fixture_values_by_all_three_steps()
    {
        var resolver = await ResolverAsync(withMappings: true);

        Assert.Equal(ResolutionStep.ExactName, resolver.Resolve(CatalogFamilies.Language, "Inglés").Step);
        Assert.Equal(ResolutionStep.NormalizedCode, resolver.Resolve(CatalogFamilies.Language, "ingles").Step);
        Assert.Equal(ResolutionStep.OperatorMapping, resolver.Resolve(CatalogFamilies.Language, "Ingl.").Step);
        Assert.False(resolver.Resolve(CatalogFamilies.Language, "Klingon").Resolved);
    }

    [Fact]
    public async Task All_three_paths_reach_the_same_catalog_entry()
    {
        var resolver = await ResolverAsync(withMappings: true);

        var exact = resolver.Resolve(CatalogFamilies.Language, "Inglés").CatalogItemId;
        Assert.NotNull(exact);
        Assert.Equal(exact, resolver.Resolve(CatalogFamilies.Language, "ingles").CatalogItemId);
        Assert.Equal(exact, resolver.Resolve(CatalogFamilies.Language, "Ingl.").CatalogItemId);
    }

    [Fact]
    public async Task Without_the_mapping_file_the_abbreviation_is_reported_as_unresolved()
    {
        var resolver = await ResolverAsync(withMappings: false);

        Assert.False(resolver.Resolve(CatalogFamilies.Language, "Ingl.").Resolved);
        Assert.Contains(resolver.UnresolvedValues, value => value.Value == "Ingl.");
    }

    [Fact]
    public async Task Every_fixture_language_resolves_except_the_one_meant_not_to()
    {
        var resolver = await ResolverAsync(withMappings: true);

        foreach (var row in LanguageRows())
        {
            var resolved = resolver.Resolve(CatalogFamilies.Language, row["Language"]).Resolved;
            Assert.Equal(row["Language"] != "Klingon", resolved);
        }

        var unresolved = Assert.Single(resolver.UnresolvedValues);
        Assert.Equal("Klingon", unresolved.Value);
        Assert.Empty(resolver.BrokenMappings);
    }

    [Fact]
    public async Task Resolution_never_adds_a_catalog_value()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(database.ConnectionString)
            .Options;
        await using var dbContext = new ApplicationDbContext(options);
        await DatabaseInitializer.MigrateAsync(dbContext, CancellationToken.None);
        await DatabaseInitializer.SeedCatalogsAsync(dbContext, CancellationToken.None);
        var before = await dbContext.CatalogItems.CountAsync();

        var resolver = await ResolverAsync(withMappings: true);
        foreach (var row in LanguageRows())
        {
            resolver.Resolve(CatalogFamilies.Language, row["Language"]);
        }

        Assert.Equal(before, await dbContext.CatalogItems.CountAsync());
    }

    [Fact]
    public async Task The_fixture_level_and_sector_values_resolve_against_their_own_families()
    {
        var resolver = await ResolverAsync(withMappings: true);

        Assert.True(resolver.Resolve(CatalogFamilies.LanguageLevel, "C1").Resolved);
        Assert.True(resolver.Resolve(CatalogFamilies.ProgramLevel, "Avanzado").Resolved);
        Assert.True(resolver.Resolve(CatalogFamilies.SkillLevel, "Alto").Resolved);
        Assert.True(resolver.Resolve(CatalogFamilies.EducationType, "Grado").Resolved);
        Assert.True(resolver.Resolve(CatalogFamilies.EducationStatus, "Finalizada").Resolved);
        Assert.True(resolver.Resolve(CatalogFamilies.Sector, "Tecnología").Resolved);
        Assert.True(resolver.Resolve(CatalogFamilies.Skill, "Gestión documental").Resolved);
        Assert.True(resolver.Resolve(CatalogFamilies.Program, "Power BI").Resolved);
    }

    [Fact]
    public async Task A_level_from_one_family_does_not_resolve_in_another()
    {
        var resolver = await ResolverAsync(withMappings: true);

        // "Medio" exists in program_level and skill_level but not language_level. The
        // composite foreign key would reject a mistake here, but resolution must not make
        // one in the first place.
        Assert.False(resolver.Resolve(CatalogFamilies.LanguageLevel, "Medio").Resolved);
        Assert.True(resolver.Resolve(CatalogFamilies.ProgramLevel, "Medio").Resolved);
    }
}
