using KeplerTalento.Domain.Catalogs;
using Xunit;

namespace KeplerTalento.Tests.UnitTests.Domain;

public sealed class CatalogItemColorTests
{
    private static readonly DateTimeOffset Created = new(2026, 10, 7, 9, 0, 0, TimeSpan.Zero);

    private static CatalogItem NewItem(string family = CatalogFamilies.Skill) =>
        new(Guid.CreateVersion7(), family, "ANALISIS", "Análisis", null, 1, Created);

    [Fact]
    public void A_new_value_holds_the_default_colour()
    {
        Assert.Equal(CatalogColors.Orange, NewItem().Color);
        Assert.Equal(CatalogColors.Orange, CatalogColors.Default);
    }

    [Fact]
    public void A_new_value_can_be_created_with_a_colour()
    {
        var item = new CatalogItem(
            Guid.CreateVersion7(), CatalogFamilies.Tag, "URGENTE", "Urgente", null, 1, Created, CatalogColors.Pink);

        Assert.Equal(CatalogColors.Pink, item.Color);
    }

    [Fact]
    public void Recolor_changes_the_colour_and_the_update_time()
    {
        var item = NewItem();
        var later = Created.AddHours(1);

        item.Recolor(CatalogColors.Blue, later);

        Assert.Equal(CatalogColors.Blue, item.Color);
        Assert.Equal(later, item.UpdatedAtUtc);
    }

    [Fact]
    public void Recolor_to_the_same_colour_leaves_the_update_time_alone()
    {
        var item = NewItem();

        item.Recolor(CatalogColors.Orange, Created.AddHours(1));

        Assert.Equal(Created, item.UpdatedAtUtc);
    }

    [Fact]
    public void The_palette_has_nine_distinct_tokens_starting_with_the_default()
    {
        Assert.Equal(9, CatalogColors.All.Count);
        Assert.Equal(9, CatalogColors.All.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(CatalogColors.Default, CatalogColors.All[0]);
    }

    [Theory]
    [InlineData("orange", true)]
    [InlineData("grey", true)]
    [InlineData("Blue", false)]
    [InlineData("magenta", false)]
    [InlineData("#ff0000", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsKnown_accepts_only_palette_tokens(string? color, bool expected)
    {
        Assert.Equal(expected, CatalogColors.IsKnown(color));
    }

    [Theory]
    [InlineData(CatalogFamilies.Skill, true)]
    [InlineData(CatalogFamilies.Language, true)]
    [InlineData(CatalogFamilies.Program, true)]
    [InlineData(CatalogFamilies.Tag, true)]
    [InlineData(CatalogFamilies.LanguageLevel, false)]
    [InlineData(CatalogFamilies.ProgramLevel, false)]
    [InlineData(CatalogFamilies.SkillLevel, false)]
    [InlineData(CatalogFamilies.EducationType, false)]
    [InlineData(CatalogFamilies.EducationStatus, false)]
    [InlineData(CatalogFamilies.Sector, false)]
    [InlineData(null, false)]
    public void Only_the_chip_families_support_a_colour(string? family, bool expected)
    {
        Assert.Equal(expected, CatalogColors.Supports(family));
    }
}
