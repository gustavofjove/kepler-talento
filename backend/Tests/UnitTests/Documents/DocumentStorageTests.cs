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

    [Fact]
    public void Scan_states_download_name_and_clamav_results_fail_closed()
    {
        var document = new CandidateDocument(Guid.NewGuid(), Guid.NewGuid(), "opaque", "../cv.pdf", "application/pdf", 12, new string('a', 64), DateTimeOffset.UtcNow);
        Assert.Equal(DocumentScanState.PendingScan, document.ScanState);
        document.MarkUnavailable(DocumentScanState.ScanFailed, "scanner.timeout", DateTimeOffset.UtcNow);
        document.MarkClean("signature", DateTimeOffset.UtcNow);
        Assert.Equal(DocumentScanState.Clean, document.ScanState);
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

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }
}
