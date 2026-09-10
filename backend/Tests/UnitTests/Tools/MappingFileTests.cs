using KeplerTalento.Domain.Catalogs;
using KeplerTalento.Tools.DataMigration.Resolution;
using Xunit;

namespace KeplerTalento.Tests.UnitTests.Tools;

public sealed class MappingFileTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"ktl-mappings-{Guid.NewGuid():N}");

    public MappingFileTests() => Directory.CreateDirectory(_directory);

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private string WriteMappings(string content)
    {
        var path = Path.Combine(_directory, "mappings.csv");
        File.WriteAllText(path, content);
        return path;
    }

    [Fact]
    public void No_mapping_file_is_not_an_error()
    {
        Assert.True(MappingFile.TryRead(null, out var mappings, out var problems));
        Assert.Empty(mappings);
        Assert.Empty(problems);
    }

    [Fact]
    public void A_well_formed_mapping_is_read()
    {
        var path = WriteMappings("Family,SourceValue,TargetCode\nlanguage,Ingl.,INGLES\n");

        Assert.True(MappingFile.TryRead(path, out var mappings, out var problems), string.Join("; ", problems));

        Assert.Equal("INGLES", mappings[(CatalogFamilies.Language, "Ingl.")]);
    }

    [Fact]
    public void An_unknown_family_is_reported()
    {
        var path = WriteMappings("Family,SourceValue,TargetCode\nidioma,Ingl.,INGLES\n");

        Assert.False(MappingFile.TryRead(path, out _, out var problems));
        Assert.Contains(problems, problem => problem.Detail.Contains("unknown family", StringComparison.Ordinal));
    }

    [Fact]
    public void A_value_mapped_twice_is_an_ambiguity_only_the_operator_can_settle()
    {
        var path = WriteMappings(
            "Family,SourceValue,TargetCode\nlanguage,Ingl.,INGLES\nlanguage,Ingl.,FRANCES\n");

        Assert.False(MappingFile.TryRead(path, out _, out var problems));
        Assert.Contains(problems, problem => problem.Detail.Contains("mapped more than once", StringComparison.Ordinal));
    }

    [Fact]
    public void The_same_value_may_be_mapped_in_two_different_families()
    {
        var path = WriteMappings(
            "Family,SourceValue,TargetCode\nlanguage,Otro,INGLES\nsector,Otro,SERVICIOS\n");

        Assert.True(MappingFile.TryRead(path, out var mappings, out var problems), string.Join("; ", problems));
        Assert.Equal(2, mappings.Count);
    }

    [Fact]
    public void A_row_missing_its_value_or_target_is_reported()
    {
        var path = WriteMappings("Family,SourceValue,TargetCode\nlanguage,,INGLES\nlanguage,Ingl.,\n");

        Assert.False(MappingFile.TryRead(path, out _, out var problems));
        Assert.Equal(2, problems.Count);
    }

    [Fact]
    public void A_missing_column_is_reported()
    {
        var path = WriteMappings("Family,SourceValue\nlanguage,Ingl.\n");

        Assert.False(MappingFile.TryRead(path, out _, out var problems));
        Assert.Contains(problems, problem => problem.Detail.Contains("TargetCode", StringComparison.Ordinal));
    }

    [Fact]
    public void The_mapping_file_cannot_create_catalog_vocabulary()
    {
        // It maps onto a code; whether that code exists is the resolver's business, and a
        // mapping pointing nowhere becomes a broken mapping rather than a new catalog value.
        var path = WriteMappings("Family,SourceValue,TargetCode\nlanguage,Klingon,KLINGON\n");
        Assert.True(MappingFile.TryRead(path, out var mappings, out _));

        var resolver = new CatalogResolver(
            [new CatalogResolver.CatalogEntry(Guid.NewGuid(), CatalogFamilies.Language, "INGLES", "Inglés")],
            mappings);

        Assert.False(resolver.Resolve(CatalogFamilies.Language, "Klingon").Resolved);
        Assert.Single(resolver.BrokenMappings);
    }
}
