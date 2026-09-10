using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.Text;
using KeplerTalento.Application.Abstractions.Documents;
using KeplerTalento.Domain.Documents;
using KeplerTalento.Infrastructure.Documents;
using Xunit;

namespace KeplerTalento.Tests.UnitTests.Documents;

public sealed class DocumentStorageTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"ktl-storage-{Guid.NewGuid():N}");

    [Fact]
    public void Opaque_keys_are_contained_and_reject_traversal()
    {
        var key = DocumentStorageKey.Create(Guid.NewGuid(), Guid.NewGuid());
        Assert.StartsWith("candidates/", key, StringComparison.Ordinal);
        Assert.DoesNotContain(".pdf", key, StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith(Path.GetFullPath(_root), DocumentStorageKey.ResolveContained(_root, key), StringComparison.OrdinalIgnoreCase);
        Assert.Throws<InvalidOperationException>(() => DocumentStorageKey.ResolveContained(_root, "../outside"));
        Assert.Throws<InvalidOperationException>(() => DocumentStorageKey.ResolveContained(_root, "C:\\outside"));
    }

    [Fact]
    public async Task Quarantine_write_hash_promote_and_collision_are_bounded()
    {
        var storage = CreateStorage();
        var key = DocumentStorageKey.Create(Guid.NewGuid(), Guid.NewGuid());
        var bytes = Encoding.UTF8.GetBytes("synthetic-cv");
        var stored = await storage.WriteQuarantineAsync(key, new MemoryStream(bytes), 100, CancellationToken.None);
        Assert.Equal(bytes.Length, stored.Size);
        Assert.Equal(64, stored.Sha256.Length);
        await Assert.ThrowsAsync<IOException>(() => storage.WriteQuarantineAsync(key, new MemoryStream(bytes), 100, CancellationToken.None));
        await storage.PromoteAsync(key, CancellationToken.None);
        await storage.PromoteAsync(key, CancellationToken.None);
        Assert.True(await storage.AvailableExistsAsync(key, CancellationToken.None));
    }

    [Fact]
    public async Task Oversized_and_empty_writes_leave_no_quarantine_file()
    {
        var storage = CreateStorage();
        var key = DocumentStorageKey.Create(Guid.NewGuid(), Guid.NewGuid());
        await Assert.ThrowsAsync<InvalidOperationException>(() => storage.WriteQuarantineAsync(key, new MemoryStream(new byte[5]), 4, CancellationToken.None));
        await Assert.ThrowsAsync<InvalidOperationException>(() => storage.WriteQuarantineAsync(key, new MemoryStream(), 4, CancellationToken.None));
        await Assert.ThrowsAsync<FileNotFoundException>(() => storage.OpenQuarantineAsync(key, CancellationToken.None));
    }

    [Theory]
    [InlineData("cv.pdf", "%PDF-1.7\nsynthetic", true)]
    [InlineData("cv.txt", "Synthetic curriculum", true)]
    [InlineData("cv.rtf", "{\\rtf1 synthetic}", true)]
    [InlineData("cv.exe", "MZ", false)]
    [InlineData("cv.pdf", "MZ", false)]
    public async Task Inspector_enforces_extension_and_content(string fileName, string content, bool accepted)
    {
        var result = await new DocumentContentInspector().InspectAsync(fileName, new MemoryStream(Encoding.UTF8.GetBytes(content)), CancellationToken.None);
        Assert.Equal(accepted, result.Accepted);
    }

    [Fact]
    public async Task Inspector_accepts_docx_but_rejects_macro_content()
    {
        await using var safe = CreateDocx(false);
        await using var macro = CreateDocx(true);
        var inspector = new DocumentContentInspector();
        Assert.True((await inspector.InspectAsync("cv.docx", safe, CancellationToken.None)).Accepted);
        Assert.False((await inspector.InspectAsync("cv.docx", macro, CancellationToken.None)).Accepted);
    }

    public static TheoryData<string, byte[]> AllowedDocuments => new()
    {
        { "cv.pdf", "%PDF-1.7\nsynthetic"u8.ToArray() },
        { "cv.doc", [0xd0, 0xcf, 0x11, 0xe0, 0xa1, 0xb1, 0x1a, 0xe1] },
        { "cv.docx", CreateOfficeZip("word/document.xml") },
        { "cv.odt", CreateOfficeZip("mimetype", "application/vnd.oasis.opendocument.text") },
        { "cv.rtf", "{\\rtf1 synthetic}"u8.ToArray() },
        { "cv.txt", "Synthetic curriculum"u8.ToArray() },
        { "cv.jpg", [0xff, 0xd8, 0xff, 0xdb] },
        { "cv.png", [0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a] },
        { "cv.tiff", "II*\0synthetic"u8.ToArray() },
        { "cv.bmp", "BMsynthetic"u8.ToArray() },
    };

    [Theory]
    [MemberData(nameof(AllowedDocuments))]
    public async Task Inspector_accepts_every_allowed_document_class(string fileName, byte[] content)
    {
        var result = await new DocumentContentInspector().InspectAsync(
            fileName,
            new MemoryStream(content),
            CancellationToken.None);
        Assert.True(result.Accepted, $"{fileName} was refused with {result.Code}");
    }

    public static TheoryData<string, byte[]> RefusedDocuments => new()
    {
        { "cv.exe", "MZ executable"u8.ToArray() },
        { "cv.sh", "#!/bin/sh"u8.ToArray() },
        { "cv.html", "<html>"u8.ToArray() },
        { "cv.svg", "<svg>"u8.ToArray() },
        { "cv.zip", CreateOfficeZip("payload") },
        { "cv.docx", CreateOfficeZip("EncryptedPackage") },
        { "cv.docx", CreateOfficeZip("word/document.xml", entryName2: "word/vbaProject.bin") },
        { "cv.docm", CreateOfficeZip("word/document.xml") },
        { "cv.pdf", "MZ extension mismatch"u8.ToArray() },
    };

    [Theory]
    [MemberData(nameof(RefusedDocuments))]
    public async Task Inspector_refuses_unsafe_disallowed_and_mismatched_documents(string fileName, byte[] content)
    {
        var result = await new DocumentContentInspector().InspectAsync(
            fileName,
            new MemoryStream(content),
            CancellationToken.None);
        Assert.False(result.Accepted, $"{fileName} was unexpectedly accepted");
    }

    [Fact]
    public void Scan_states_download_name_and_clamav_results_fail_closed()
    {
        var document = new CandidateDocument(Guid.NewGuid(), Guid.NewGuid(), "opaque", "../cv.pdf", "application/pdf", 12, new string('a', 64), DateTimeOffset.UtcNow);
        Assert.Equal(DocumentScanState.PendingScan, document.ScanState);
        document.MarkUnavailable(DocumentScanState.ScanFailed, "scanner.timeout", DateTimeOffset.UtcNow);
        Assert.Throws<InvalidOperationException>(() => document.MarkClean("signature", DateTimeOffset.UtcNow));
        Assert.Equal(DocumentScanState.ScanFailed, document.ScanState);
        Assert.Equal("_cv.pdf", DocumentDownloadService.SanitizeFileName("../cv.pdf"));
        Assert.Equal(ScanVerdict.Clean, ClamAvScanner.ParseResponse("stream: OK").Verdict);
        Assert.Equal(ScanVerdict.Infected, ClamAvScanner.ParseResponse("stream: Eicar-Test-Signature FOUND").Verdict);
        Assert.Equal(ScanVerdict.Error, ClamAvScanner.ParseResponse("stream: size limit exceeded ERROR").Verdict);
    }

    [Fact]
    public async Task Clamav_adapter_maps_a_stalled_daemon_to_timeout()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            var scanner = new ClamAvScanner(new ClamAvOptions { Host = "127.0.0.1", Port = port, TimeoutSeconds = 1 });
            var scan = scanner.ScanAsync(new MemoryStream("synthetic"u8.ToArray()), CancellationToken.None);
            using var accepted = await listener.AcceptTcpClientAsync();

            var result = await scan;

            Assert.Equal(ScanVerdict.Error, result.Verdict);
            Assert.Equal("scanner.timeout", result.Code);
        }
        finally
        {
            listener.Stop();
        }
    }

    private FileSystemDocumentStorage CreateStorage()
    {
        var options = new DocumentStorageOptions { Root = _root };
        FileSystemDocumentStorage.ValidateAndPrepare(options);
        return new(options);
    }

    private static MemoryStream CreateDocx(bool macro)
    {
        var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            archive.CreateEntry("[Content_Types].xml");
            archive.CreateEntry("word/document.xml");
            if (macro) archive.CreateEntry("word/vbaProject.bin");
        }
        stream.Position = 0;
        return stream;
    }

    private static byte[] CreateOfficeZip(string entryName, string? entryContent = null, string? entryName2 = null)
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            archive.CreateEntry("[Content_Types].xml");
            var entry = archive.CreateEntry(entryName);
            if (entryContent is not null)
            {
                using var writer = new StreamWriter(entry.Open(), Encoding.ASCII);
                writer.Write(entryContent);
            }
            if (entryName2 is not null) archive.CreateEntry(entryName2);
        }
        return stream.ToArray();
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }
}
