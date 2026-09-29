using System.IO.Compression;
using System.Text;
using KeplerTalento.Application.Abstractions.CvExtraction;
using KeplerTalento.Infrastructure.CvExtraction;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;
using Xunit;

namespace KeplerTalento.Tests.UnitTests.CvExtraction;

/// <summary>
/// KTL-32 readers over documents generated in the test, so no real CV is ever needed.
/// </summary>
public sealed class CvTextReaderTests
{
    private static readonly CvReadBounds Bounds = new(5, 50_000);
    private static readonly CvTextReader Reader = new(new PdfCvTextReader(), new DocxCvTextReader(), new CvDraftOptions { MaxConcurrent = 32 });

    [Fact]
    public async Task A_pdf_is_read_top_to_bottom_with_point_sizes()
    {
        var pdf = Pdf(page =>
        {
            page.Text("Anselmo Quintana Robles", 24, 50, 780);
            page.Text("anselmo.quintana@example.test", 10, 50, 750);
            page.Text("Experiencia", 14, 50, 700);
        });

        var text = await ReadAsync(CvFileKind.Pdf, pdf);

        Assert.Equal(["Anselmo Quintana Robles", "anselmo.quintana@example.test", "Experiencia"], text.Lines.Select(line => line.Text));
        Assert.Equal(24, text.Lines[0].FontSize!.Value, 0);
        Assert.Equal(10, text.Lines[1].FontSize!.Value, 0);
        Assert.Equal(1, text.PageCount);
    }

    [Fact]
    public async Task Two_columns_on_the_same_baseline_stay_separate_lines()
    {
        var pdf = Pdf(page =>
        {
            page.Text("Contacto", 12, 40, 700);
            page.Text("Experiencia profesional", 12, 320, 700);
        });

        var text = await ReadAsync(CvFileKind.Pdf, pdf);

        Assert.Equal(["Contacto", "Experiencia profesional"], text.Lines.Select(line => line.Text));
    }

    [Fact]
    public async Task A_pdf_without_a_text_layer_has_no_text()
    {
        var pdf = Pdf(page => page.Rectangle());

        var text = await ReadAsync(CvFileKind.Pdf, pdf);

        Assert.False(text.HasText);
    }

    [Fact]
    public async Task Only_the_first_pages_are_read()
    {
        var pdf = Pdf(Enumerable.Range(1, 8).Select(number => (Action<PageWriter>)(page => page.Text($"Pagina {number}", 12, 50, 700))).ToArray());

        var text = await Reader.ReadAsync(CvFileKind.Pdf, new MemoryStream(pdf), new CvReadBounds(3, 50_000), CancellationToken.None);

        Assert.Equal(3, text.PageCount);
        Assert.Equal(["Pagina 1", "Pagina 2", "Pagina 3"], text.Lines.Select(line => line.Text));
        Assert.Equal([0, 1, 2], text.Lines.Select(line => line.Page));
    }

    [Fact]
    public async Task Characters_beyond_the_bound_are_not_read()
    {
        var pdf = Pdf(page =>
        {
            page.Text("Primera linea larga", 12, 50, 700);
            page.Text("Segunda linea", 12, 50, 680);
        });

        var text = await Reader.ReadAsync(CvFileKind.Pdf, new MemoryStream(pdf), new CvReadBounds(5, 1_000), CancellationToken.None);
        var capped = await Reader.ReadAsync(CvFileKind.Pdf, new MemoryStream(pdf), new CvReadBounds(5, 10), CancellationToken.None);

        Assert.Equal(2, text.Lines.Count);
        Assert.Equal(10, capped.Lines.Sum(line => line.Text.Length));
    }

    [Fact]
    public async Task A_malformed_pdf_is_unreadable_without_leaking_the_parser_message()
    {
        var content = Encoding.ASCII.GetBytes("%PDF-1.7\nZoraida Villalobos not really a pdf");

        var failure = await Assert.ThrowsAsync<CvUnreadableException>(() => ReadAsync(CvFileKind.Pdf, content));

        Assert.DoesNotContain("Zoraida", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_password_protected_pdf_is_unreadable()
    {
        // A real PDF whose trailer declares standard-handler encryption with a user password the
        // reader does not have: the content checks let it through, the reader must refuse it.
        var text = Encoding.Latin1.GetString(Pdf(page => page.Text("Texto protegido", 12, 50, 700)));
        var zeros = new string('0', 64);
        var encrypted = text.Replace(
            "trailer\n<<",
            $"trailer\n<< /Encrypt << /Filter /Standard /V 2 /R 3 /Length 128 /P -4 /O <{zeros}> /U <{zeros}> >> /ID [<{zeros[..32]}> <{zeros[..32]}>]",
            StringComparison.Ordinal);
        Assert.NotEqual(text, encrypted);

        var failure = await Assert.ThrowsAsync<CvUnreadableException>(
            () => ReadAsync(CvFileKind.Pdf, Encoding.Latin1.GetBytes(encrypted)));

        Assert.DoesNotContain("protegido", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_docx_is_read_one_line_per_paragraph_headers_first()
    {
        var docx = Docx(
            header: Paragraph("Anselmo Quintana Robles", halfPoints: 48),
            body: Paragraph("anselmo.quintana@example.test") + Paragraph("Móvil:", "611 98 76 54") + Paragraph(""));

        var text = await ReadAsync(CvFileKind.Docx, docx);

        Assert.Equal(["Anselmo Quintana Robles", "anselmo.quintana@example.test", "Móvil: 611 98 76 54"], text.Lines.Select(line => line.Text));
        Assert.Equal(24, text.Lines[0].FontSize);
        Assert.Null(text.Lines[1].FontSize);
    }

    [Fact]
    public async Task A_docx_without_a_body_is_unreadable()
    {
        using var buffer = new MemoryStream();
        using (var archive = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            Write(archive, "[Content_Types].xml", "<Types/>");
        }

        await Assert.ThrowsAsync<CvUnreadableException>(() => ReadAsync(CvFileKind.Docx, buffer.ToArray()));
    }

    [Fact]
    public async Task A_docx_declaring_a_dtd_is_unreadable()
    {
        var body = """<?xml version="1.0"?><!DOCTYPE d [<!ENTITY x "boom">]><w:document xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main"><w:body><w:p><w:r><w:t>&x;</w:t></w:r></w:p></w:body></w:document>""";
        using var buffer = new MemoryStream();
        using (var archive = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            Write(archive, "word/document.xml", body);
        }

        await Assert.ThrowsAsync<CvUnreadableException>(() => ReadAsync(CvFileKind.Docx, buffer.ToArray()));
    }

    [Fact]
    public async Task A_cancelled_read_stops()
    {
        var pdf = Pdf(page => page.Text("Texto", 12, 50, 700));
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => Reader.ReadAsync(CvFileKind.Pdf, new MemoryStream(pdf), Bounds, cancelled.Token));
    }

    [Fact]
    public async Task A_timed_out_parser_keeps_its_slot_until_it_really_stops()
    {
        using var stuck = new ManualResetEventSlim();
        using var reader = new CvTextReader(
            (_, _, _, _) =>
            {
                // A parser blocked inside one page: it never looks at the token.
                stuck.Wait(TimeSpan.FromSeconds(30));
                return CvText.Empty;
            },
            maxConcurrent: 1);
        using var budget = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => reader.ReadAsync(CvFileKind.Pdf, new MemoryStream(), Bounds, budget.Token));

        // The request gave up, but the worker still runs, so it still holds the only slot.
        await Assert.ThrowsAsync<CvReaderBusyException>(
            () => reader.ReadAsync(CvFileKind.Pdf, new MemoryStream(), Bounds, CancellationToken.None));

        stuck.Set();
        var freed = await RetryUntilFreeAsync(reader);
        Assert.False(freed.HasText);
    }

    [Fact]
    public async Task A_read_cancelled_before_it_starts_releases_its_slot()
    {
        using var reader = new CvTextReader((_, _, _, _) => CvText.Empty, maxConcurrent: 1);
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => reader.ReadAsync(CvFileKind.Pdf, new MemoryStream(), Bounds, cancelled.Token));

        await RetryUntilFreeAsync(reader);
    }

    private static async Task<CvText> RetryUntilFreeAsync(CvTextReader reader)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                return await reader.ReadAsync(CvFileKind.Pdf, new MemoryStream(), Bounds, CancellationToken.None);
            }
            catch (CvReaderBusyException) when (attempt < 50)
            {
                await Task.Delay(20);
            }
        }
    }

    private static Task<CvText> ReadAsync(CvFileKind kind, byte[] content) =>
        Reader.ReadAsync(kind, new MemoryStream(content), Bounds, CancellationToken.None);

    internal sealed class PageWriter(PdfPageBuilder page, PdfDocumentBuilder.AddedFont font)
    {
        public void Text(string text, double size, double x, double y) =>
            page.AddText(text, size, new PdfPoint(x, y), font);

        public void Rectangle() => page.DrawRectangle(new PdfPoint(50, 50), 200, 300);
    }

    internal static byte[] Pdf(params Action<PageWriter>[] pages)
    {
        var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        foreach (var draw in pages)
        {
            draw(new PageWriter(builder.AddPage(PageSize.A4), font));
        }
        return builder.Build();
    }

    internal static string Paragraph(string text, int? halfPoints = null) =>
        $"<w:p><w:r>{(halfPoints is { } size ? $"<w:rPr><w:sz w:val=\"{size}\"/></w:rPr>" : string.Empty)}<w:t xml:space=\"preserve\">{System.Security.SecurityElement.Escape(text)}</w:t></w:r></w:p>";

    internal static string Paragraph(string first, string second) =>
        $"<w:p><w:r><w:t>{first}</w:t></w:r><w:r><w:tab/><w:t>{second}</w:t></w:r></w:p>";

    internal static byte[] Docx(string body, string? header = null)
    {
        const string ns = "xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"";
        using var buffer = new MemoryStream();
        using (var archive = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            Write(archive, "[Content_Types].xml", "<?xml version=\"1.0\"?><Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"/>");
            Write(archive, "word/document.xml", $"<?xml version=\"1.0\"?><w:document {ns}><w:body>{body}</w:body></w:document>");
            if (header is not null)
            {
                Write(archive, "word/header1.xml", $"<?xml version=\"1.0\"?><w:hdr {ns}>{header}</w:hdr>");
            }
        }
        return buffer.ToArray();
    }

    private static void Write(ZipArchive archive, string name, string content)
    {
        using var writer = new StreamWriter(archive.CreateEntry(name).Open(), new UTF8Encoding(false));
        writer.Write(content);
    }
}
