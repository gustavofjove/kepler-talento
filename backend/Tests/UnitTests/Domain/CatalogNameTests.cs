using KeplerTalento.Domain.Catalogs;
using Xunit;

namespace KeplerTalento.Tests.UnitTests.Domain;

public sealed class CatalogNameTests
{
    [Theory]
    [InlineData("Inglés", "ingles")]
    [InlineData("ingles", "ingles")]
    [InlineData("  INGLÉS  ", "ingles")]
    [InlineData("Tecnología", "tecnologia")]
    [InlineData("Básico", "basico")]
    [InlineData("Atención al cliente", "atencion al cliente")]
    [InlineData("Gestión documental", "gestion documental")]
    public void Normalize_trims_case_folds_and_strips_accents(string input, string expected)
    {
        Assert.Equal(expected, CatalogName.Normalize(input));
    }

    [Theory]
    [InlineData("Inglés", "ingles")]
    [InlineData("INGLES", "ingles")]
    [InlineData(" inglés ", "ingles")]
    public void Names_that_differ_only_by_case_or_accent_collide(string left, string right)
    {
        Assert.Equal(CatalogName.Normalize(left), CatalogName.Normalize(right));
    }

    [Theory]
    [InlineData("Portugués", "portugues")]
    [InlineData("Alemán", "aleman")]
    [InlineData("Educación", "educacion")]
    [InlineData("Diseño", "diseno")]
    [InlineData("Français", "francais")]
    public void Normalize_folds_accents_without_relying_on_unicode_decomposition(
        string input,
        string expected)
    {
        // The API runs with invariant globalization, where string.Normalize is a no-op,
        // so the fold must not depend on it.
        Assert.Equal(expected, CatalogName.Normalize(input));
    }

    [Fact]
    public void Different_names_do_not_collide()
    {
        Assert.NotEqual(CatalogName.Normalize("Inglés"), CatalogName.Normalize("Francés"));
    }

    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("   ", "")]
    public void Blank_names_normalize_to_empty(string? input, string expected)
    {
        Assert.Equal(expected, CatalogName.Normalize(input));
    }

    [Theory]
    [InlineData("Inglés", "INGLES")]
    [InlineData("Power BI", "POWER_BI")]
    [InlineData("Atención al cliente", "ATENCION_AL_CLIENTE")]
    [InlineData("Tecnología", "TECNOLOGIA")]
    [InlineData("C2", "C2")]
    [InlineData("!!!", "ITEM")]
    public void DeriveCode_produces_an_uppercase_ascii_slug(string input, string expected)
    {
        Assert.Equal(expected, CatalogName.DeriveCode(input));
    }
}
