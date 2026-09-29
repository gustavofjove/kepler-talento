using KeplerTalento.Application.Abstractions.CvExtraction;
using KeplerTalento.Infrastructure.CvExtraction;
using Xunit;

namespace KeplerTalento.Tests.UnitTests.CvExtraction;

/// <summary>
/// The KTL-32 suggestion rules over synthetic CV text. Every person here is fictitious.
/// </summary>
public sealed class RuleBasedCandidateDraftExtractorTests
{
    private static readonly RuleBasedCandidateDraftExtractor Extractor = new(new SpanishPlaces());

    [Fact]
    public void A_typical_contact_block_yields_every_field()
    {
        var draft = Extract(
            ("Zoraida Villalobos Etxeberria", 22),
            ("Desarrolladora backend", 12),
            ("zoraida.villalobos@example.test", 10),
            ("+34 611 987 654", 10),
            ("Calle Mayor 3, 48001 Bilbao", 10),
            ("Experiencia", 14),
            ("Empresa Ficticia, 28001 Madrid", 10));

        Assert.Equal("Zoraida", draft.FirstName?.Value);
        Assert.Equal("Villalobos Etxeberria", draft.LastName?.Value);
        Assert.Equal(SuggestionConfidence.High, draft.FirstName?.Confidence);
        Assert.Equal("zoraida.villalobos@example.test", draft.Email?.Value);
        Assert.Equal("611 98 76 54", draft.Phone?.Value);
        Assert.Equal(SuggestionConfidence.High, draft.Phone?.Confidence);
        Assert.Equal("Bilbao", draft.Location?.Value);
        Assert.Equal(SuggestionConfidence.High, draft.Location?.Confidence);
        Assert.Equal("Bizkaia", draft.Province?.Value);
    }

    [Fact]
    public void The_largest_text_near_the_top_is_the_name_not_the_first_line()
    {
        var draft = Extract(
            ("Curriculum Vitae", 12),
            ("Desarrollador Full Stack", 14),
            ("Anselmo Quintana Robles", 26),
            ("anselmo.quintana@example.test", 10));

        Assert.Equal("Anselmo", draft.FirstName?.Value);
        Assert.Equal("Quintana Robles", draft.LastName?.Value);
    }

    [Fact]
    public void A_name_the_email_does_not_mention_is_low_confidence()
    {
        var draft = Extract(("Anselmo Quintana Robles", 24), ("contacto@example.test", 10));

        Assert.Equal(SuggestionConfidence.Low, draft.FirstName?.Confidence);
        Assert.Equal(SuggestionConfidence.Low, draft.LastName?.Confidence);
    }

    [Fact]
    public void Four_words_are_two_given_names_and_two_surnames()
    {
        var draft = Extract(("María José Quintana Robles", 24), ("mjquintana@example.test", 10));

        Assert.Equal("María José", draft.FirstName?.Value);
        Assert.Equal("Quintana Robles", draft.LastName?.Value);
        Assert.Equal(SuggestionConfidence.High, draft.FirstName?.Confidence);
    }

    [Fact]
    public void An_email_running_given_names_together_fixes_the_split()
    {
        var draft = Extract(("Juan Carlos Quintana", 24), ("juancarlos.quintana@example.test", 10));

        Assert.Equal("Juan Carlos", draft.FirstName?.Value);
        Assert.Equal("Quintana", draft.LastName?.Value);
    }

    [Fact]
    public void Particles_stay_with_the_surname()
    {
        var draft = Extract(("Lucía de la Fuente Robles", 24), ("lucia.delafuente@example.test", 10));

        Assert.Equal("Lucía", draft.FirstName?.Value);
        Assert.Equal("de la Fuente Robles", draft.LastName?.Value);
    }

    [Fact]
    public void A_shouted_name_is_suggested_in_display_case()
    {
        var draft = Extract(("ZORAIDA VILLALOBOS-ETXEBERRIA", 24));

        Assert.Equal("Zoraida", draft.FirstName?.Value);
        Assert.Equal("Villalobos-Etxeberria", draft.LastName?.Value);
    }

    [Fact]
    public void A_shouted_name_keeps_its_accents_in_display_case()
    {
        var draft = Extract(("ÁNGELA NÚÑEZ GARCÍA-ÜBEDA", 24));

        Assert.Equal("Ángela", draft.FirstName?.Value);
        Assert.Equal("Núñez García-Übeda", draft.LastName?.Value);
    }

    [Fact]
    public void An_accented_shouted_name_is_corroborated_by_an_unaccented_email()
    {
        var draft = Extract(("CURRICULUM VITAE", 12), ("ÁNGELA NÚÑEZ GARCÍA-ÚBEDA", 24), ("angela.nunez@example.test", 10));

        Assert.Equal("Ángela", draft.FirstName?.Value);
        Assert.Equal(SuggestionConfidence.High, draft.FirstName?.Confidence);
    }

    [Theory]
    [InlineData("Madrid Barcelona")]
    [InlineData("Datos Personales")]
    [InlineData("Ingeniero de Software")]
    public void Headings_titles_and_places_are_not_names(string line)
    {
        var draft = Extract((line, 30));

        Assert.Null(draft.FirstName);
        Assert.Null(draft.LastName);
    }

    [Fact]
    public void Without_point_sizes_the_first_plausible_line_is_the_name()
    {
        var draft = Extract(
            new CvLine(0, "Curriculum Vitae"),
            new CvLine(0, "Anselmo Quintana Robles"),
            new CvLine(0, "Rosario Pérez Gil"));

        Assert.Equal("Anselmo", draft.FirstName?.Value);
    }

    [Theory]
    [InlineData("Tel. 611 98 76 54", "611 98 76 54")]
    [InlineData("Móvil: 0034 611987654", "611 98 76 54")]
    [InlineData("Fijo (+34) 915.234.567", "915 23 45 67")]
    [InlineData("Phone +44 20 7946 0958", "+44 20 7946 0958")]
    public void Phones_are_recognised_across_formats(string line, string expected)
    {
        var draft = Extract((line, 10));

        Assert.Equal(expected, draft.Phone?.Value);
    }

    [Fact]
    public void Dates_postcodes_and_years_are_not_phones()
    {
        var draft = Extract(("2019 - 2023", 10), ("48001 Bilbao", 10), ("12/05/2021", 10));

        Assert.Null(draft.Phone);
    }

    [Fact]
    public void The_first_email_and_phone_in_the_document_win()
    {
        var draft = Extract(
            ("anselmo@example.test", 10),
            ("611 98 76 54", 10),
            ("Referencia: jefa@example.test", 10),
            ("622 11 22 33", 10));

        Assert.Equal("anselmo@example.test", draft.Email?.Value);
        Assert.Equal("611 98 76 54", draft.Phone?.Value);
    }

    [Fact]
    public void A_phone_found_only_deep_in_the_document_is_low_confidence()
    {
        var lines = Enumerable.Range(0, 40).Select(index => new CvLine(0, $"Línea de relleno número {index}")).ToList();
        lines.Add(new CvLine(0, "611 98 76 54"));

        var draft = Extractor.Extract(new CvText(lines, 1));

        Assert.Equal(SuggestionConfidence.Low, draft.Phone?.Confidence);
    }

    [Fact]
    public void A_postcode_without_a_known_municipality_still_names_the_province()
    {
        var draft = Extract(("Calle Inventada 7, 28999 Villafranca del Sentinel", 10));

        Assert.Null(draft.Location);
        Assert.Equal("Madrid", draft.Province?.Value);
        Assert.Equal(SuggestionConfidence.High, draft.Province?.Confidence);
    }

    [Fact]
    public void A_municipality_on_the_line_after_the_postcode_is_used()
    {
        var draft = Extract(("Avenida Inventada 12, 15001", 10), ("A Coruña", 10));

        Assert.Equal("A Coruña", draft.Location?.Value);
        Assert.Equal("A Coruña", draft.Province?.Value);
    }

    [Fact]
    public void A_municipality_without_a_postcode_is_low_confidence()
    {
        var draft = Extract(("Residencia: Valladolid", 10));

        Assert.Equal("Valladolid", draft.Location?.Value);
        Assert.Equal("Valladolid", draft.Province?.Value);
        Assert.Equal(SuggestionConfidence.Low, draft.Location?.Confidence);
    }

    [Fact]
    public void A_postcode_in_the_experience_section_is_not_where_the_candidate_lives()
    {
        var lines = new List<CvLine> { new(0, "Anselmo Quintana Robles", 24) };
        lines.AddRange(Enumerable.Range(0, 40).Select(index => new CvLine(0, $"Responsabilidad número {index}")));
        lines.Add(new CvLine(0, "Empresa Ficticia, 28001 Madrid"));

        var draft = Extractor.Extract(new CvText(lines, 1));

        Assert.Null(draft.Location);
        Assert.Null(draft.Province);
    }

    [Fact]
    public void A_phone_number_is_not_read_as_a_postcode()
    {
        var draft = Extract(("Teléfono 611 28001 4", 10), ("+34 628 001 234", 10));

        Assert.Null(draft.Province);
    }

    [Fact]
    public void Text_without_letters_yields_nothing()
    {
        var draft = Extractor.Extract(new CvText([new CvLine(0, "12345 ---")], 1));

        Assert.Equal(CandidateDraftSuggestions.None, draft);
    }

    private static CandidateDraftSuggestions Extract(params (string Text, double Size)[] lines) =>
        Extractor.Extract(new CvText(lines.Select(line => new CvLine(0, line.Text, line.Size)).ToList(), 1));

    private static CandidateDraftSuggestions Extract(params CvLine[] lines) =>
        Extractor.Extract(new CvText(lines, 1));
}
