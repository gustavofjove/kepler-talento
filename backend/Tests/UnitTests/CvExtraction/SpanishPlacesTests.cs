using KeplerTalento.Infrastructure.CvExtraction;
using Xunit;

namespace KeplerTalento.Tests.UnitTests.CvExtraction;

public sealed class SpanishPlacesTests
{
    private static readonly SpanishPlaces Places = new();

    [Fact]
    public void The_embedded_list_carries_every_municipality()
    {
        Assert.InRange(Places.MunicipalityCount, 8_000, 8_300);
    }

    [Theory]
    [InlineData("28001", "Madrid")]
    [InlineData("48001", "Bizkaia")]
    [InlineData("01005", "Álava")]
    [InlineData("08080", "Barcelona")]
    [InlineData("52006", "Melilla")]
    public void A_postcode_names_its_province_by_its_first_two_digits(string postcode, string province)
    {
        Assert.Equal(province, Places.ProvinceOfPostcode(postcode)?.Name);
    }

    [Theory]
    [InlineData("00123")]
    [InlineData("53001")]
    [InlineData("2800")]
    [InlineData("280011")]
    public void A_number_that_is_not_a_spanish_postcode_names_no_province(string value)
    {
        Assert.Null(Places.ProvinceOfPostcode(value));
    }

    [Theory]
    [InlineData("vizcaya", "Bizkaia")]
    [InlineData("la coruna", "A Coruña")]
    [InlineData("lerida", "Lleida")]
    [InlineData("islas baleares", "Illes Balears")]
    public void A_province_is_found_by_its_everyday_names(string normalized, string province)
    {
        Assert.Equal(province, Places.FindProvince(normalized)?.Name);
    }

    [Theory]
    [InlineData("a coruna", "15030", "A Coruña")]
    [InlineData("coruna", "15030", "A Coruña")]
    [InlineData("san sebastian", "20069", "San Sebastián")]
    [InlineData("donostia", "20069", "Donostia")]
    [InlineData("vitoria", "01059", "Vitoria")]
    [InlineData("vitoria gasteiz", "01059", "Vitoria-Gasteiz")]
    [InlineData("alicante", "03014", "Alicante")]
    public void A_municipality_is_found_by_its_ine_forms_and_aliases(string normalized, string code, string display)
    {
        var found = Places.FindMunicipalities(normalized);

        var municipality = Assert.Single(found, value => value.Code == code);
        Assert.Equal(display, municipality.Name);
        Assert.Equal(code[..2], municipality.ProvinceCode);
    }

    [Fact]
    public void An_unknown_name_is_not_a_municipality()
    {
        Assert.Empty(Places.FindMunicipalities("villafranca del sentinel"));
    }
}
