using KeplerTalento.Domain.Catalogs;
using KeplerTalento.Infrastructure.Persistence;
using Xunit;

namespace KeplerTalento.Tests.UnitTests.Persistence;

/// <summary>
/// The seed is inserted in one <c>SaveChanges</c> against unique indexes on
/// <c>(Family, Code)</c> and <c>(Family, NameNormalized)</c>. A duplicate inside a family
/// therefore fails deployment rather than a test, and only against a live database — so the
/// constraints are asserted here, where adding a value shows the clash immediately.
/// </summary>
public sealed class CatalogSeedDataTests
{
    public static TheoryData<string> Families()
    {
        var data = new TheoryData<string>();
        foreach (var family in CatalogSeedData.Families.Keys)
        {
            data.Add(family);
        }
        return data;
    }

    [Theory]
    [MemberData(nameof(Families))]
    public void Seeded_codes_are_unique_within_their_family(string family)
    {
        var duplicates = CatalogSeedData.Families[family]
            .GroupBy(value => value.ResolveCode(), StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => $"{group.Key}: {string.Join(", ", group.Select(value => value.NameEs))}")
            .ToList();

        Assert.True(duplicates.Count == 0, $"Duplicate codes in '{family}': {string.Join(" | ", duplicates)}");
    }

    [Theory]
    [MemberData(nameof(Families))]
    public void Seeded_names_are_unique_within_their_family_after_normalization(string family)
    {
        var duplicates = CatalogSeedData.Families[family]
            .GroupBy(value => CatalogName.Normalize(value.NameEs), StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => $"{group.Key}: {string.Join(", ", group.Select(value => value.NameEs))}")
            .ToList();

        Assert.True(duplicates.Count == 0, $"Duplicate names in '{family}': {string.Join(" | ", duplicates)}");
    }

    [Fact]
    public void Every_known_family_is_seeded()
    {
        Assert.Equal(
            CatalogFamilies.All.Order(StringComparer.Ordinal),
            CatalogSeedData.Families.Keys.Order(StringComparer.Ordinal));
    }
}
