using System.IO.Compression;
using System.Text;
using KeplerTalento.Domain.Import;
using KeplerTalento.Infrastructure.Import;
using Xunit;

namespace KeplerTalento.Tests.UnitTests.Import;

public sealed class ImportFileInspectorTests
{
    [Fact]
    public async Task Plain_utf8_csv_is_accepted()
    {
        var inspection = await InspectAsync(Encoding.UTF8.GetBytes("first_name,last_name,email\nMaría,Núñez,maria@example.test\n"));

        Assert.True(inspection.Accepted);
    }

    [Fact]
    public async Task A_spreadsheet_container_named_csv_is_refused()
    {
        using var zip = new MemoryStream();
        using (var archive = new ZipArchive(zip, ZipArchiveMode.Create, leaveOpen: true))
        {
            archive.CreateEntry("[Content_Types].xml");
            archive.CreateEntry("xl/vbaProject.bin");
        }

        var inspection = await InspectAsync(zip.ToArray());

        Assert.False(inspection.Accepted);
        Assert.Equal(ImportReasonCodes.FileContentMismatch, inspection.Code);
    }

    [Theory]
    [InlineData(new byte[] { 0xd0, 0xcf, 0x11, 0xe0, 0xa1, 0xb1, 0x1a, 0xe1, 0x00 })]
    [InlineData(new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2d, 0x31 })]
    [InlineData(new byte[] { 0x4d, 0x5a, 0x90, 0x00 })]
    [InlineData(new byte[] { 0x61, 0x2c, 0x00, 0x62 })]
    [InlineData(new byte[] { 0x61, 0x2c, 0x07, 0x62 })]
    public async Task Binary_content_is_refused(byte[] content)
    {
        Assert.False((await InspectAsync(content)).Accepted);
    }

    [Fact]
    public async Task Utf16_is_refused_because_the_contract_is_utf8()
    {
        var inspection = await InspectAsync(Encoding.Unicode.GetPreamble().Concat(Encoding.Unicode.GetBytes("first_name")).ToArray());

        Assert.False(inspection.Accepted);
    }

    [Fact]
    public async Task Invalid_utf8_is_refused_as_an_encoding_problem()
    {
        var inspection = await InspectAsync([0x61, 0x2c, 0xc3, 0x28, 0x0a]);

        Assert.Equal(ImportReasonCodes.FileEncodingInvalid, inspection.Code);
    }

    [Fact]
    public async Task A_multibyte_character_cut_at_the_sniffing_boundary_is_not_mistaken_for_bad_encoding()
    {
        var text = new string('a', 64 * 1024 - 1) + "ñ";

        Assert.True((await InspectAsync(Encoding.UTF8.GetBytes(text))).Accepted);
    }

    private static Task<KeplerTalento.Application.Abstractions.Import.ImportFileInspection> InspectAsync(byte[] content) =>
        new ImportFileInspector().InspectAsync(new MemoryStream(content), CancellationToken.None);
}
