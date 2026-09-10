using KeplerTalento.Tools.DataMigration.Export;
using Xunit;

namespace KeplerTalento.Tests.UnitTests.Tools;

public sealed class CsvDocumentTests
{
    [Fact]
    public void A_header_and_rows_are_keyed_by_column_name()
    {
        var document = CsvDocument.Parse("A,B\n1,2\n3,4\n");

        Assert.Equal(["A", "B"], document.Header);
        Assert.Equal(2, document.Rows.Count);
        Assert.Equal("1", document.Rows[0]["A"]);
        Assert.Equal("4", document.Rows[1]["B"]);
    }

    [Fact]
    public void Column_order_does_not_matter_because_columns_are_matched_by_name()
    {
        var document = CsvDocument.Parse("B,A\n2,1\n");

        Assert.Equal("1", document.Rows[0]["A"]);
        Assert.Equal("2", document.Rows[0]["B"]);
    }

    [Fact]
    public void A_byte_order_mark_is_stripped_from_the_first_column_name()
    {
        var document = CsvDocument.Parse("﻿SourceKey,Name\nC-001,x\n");

        Assert.Equal("SourceKey", document.Header[0]);
        Assert.Equal("C-001", document.Rows[0]["SourceKey"]);
    }

    [Theory]
    [InlineData("A,B\r\n1,2\r\n")]
    [InlineData("A,B\n1,2\n")]
    [InlineData("A,B\n1,2")]
    public void Line_endings_and_a_missing_final_newline_are_all_accepted(string text)
    {
        var document = CsvDocument.Parse(text);

        var row = Assert.Single(document.Rows);
        Assert.Equal("1", row["A"]);
        Assert.Equal("2", row["B"]);
    }

    [Fact]
    public void A_quoted_field_may_contain_commas_quotes_and_newlines()
    {
        // Candidate notes routinely contain all three.
        var document = CsvDocument.Parse("A,B\n\"uno, dos\",\"dijo \"\"hola\"\"\"\n");

        Assert.Equal("uno, dos", document.Rows[0]["A"]);
        Assert.Equal("dijo \"hola\"", document.Rows[0]["B"]);
    }

    [Fact]
    public void A_newline_inside_a_quoted_field_does_not_start_a_new_row()
    {
        var document = CsvDocument.Parse("A,B\n\"primera\nsegunda\",x\ny,z\n");

        Assert.Equal(2, document.Rows.Count);
        Assert.Equal("primera\nsegunda", document.Rows[0]["A"]);
        Assert.Equal("y", document.Rows[1]["A"]);
    }

    [Fact]
    public void Rows_carry_the_line_they_started_on_even_after_a_multi_line_field()
    {
        var document = CsvDocument.Parse("A,B\n\"uno\ndos\",x\ntres,y\n");

        Assert.Equal(2, document.Rows[0].LineNumber);
        Assert.Equal(4, document.Rows[1].LineNumber);
    }

    [Fact]
    public void A_short_row_yields_empty_values_rather_than_throwing()
    {
        var document = CsvDocument.Parse("A,B,C\n1,2\n");

        Assert.Equal("2", document.Rows[0]["B"]);
        Assert.True(document.Rows[0].IsEmpty("C"));
    }

    [Fact]
    public void Accented_characters_survive_the_round_trip()
    {
        var document = CsvDocument.Parse("Language\nInglés\nAlemán\n");

        Assert.Equal("Inglés", document.Rows[0]["Language"]);
        Assert.Equal("Alemán", document.Rows[1]["Language"]);
    }

    [Fact]
    public void An_empty_document_has_no_header_and_no_rows()
    {
        Assert.Empty(CsvDocument.Parse(string.Empty).Header);
        Assert.Empty(CsvDocument.Parse(string.Empty).Rows);
    }

    [Fact]
    public void A_header_only_file_is_valid_and_contributes_no_rows()
    {
        var document = CsvDocument.Parse("SourceKey,Name\n");

        Assert.Equal(2, document.Header.Count);
        Assert.Empty(document.Rows);
    }
}
