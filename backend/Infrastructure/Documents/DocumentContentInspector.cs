using System.IO.Compression;
using System.Text;
using KeplerTalento.Application.Abstractions.Documents;

namespace KeplerTalento.Infrastructure.Documents;

public sealed class DocumentContentInspector : IDocumentContentInspector
{
    private static readonly Dictionary<string, string> ContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"] = "application/pdf",
        [".doc"] = "application/msword",
        [".docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        [".odt"] = "application/vnd.oasis.opendocument.text",
        [".rtf"] = "application/rtf",
        [".txt"] = "text/plain",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".png"] = "image/png",
        [".tif"] = "image/tiff",
        [".tiff"] = "image/tiff",
        [".bmp"] = "image/bmp",
    };

    public async Task<InspectedDocument> InspectAsync(string fileName, Stream content, CancellationToken cancellationToken)
    {
        var extension = Path.GetExtension(fileName);
        if (!ContentTypes.TryGetValue(extension, out var contentType)) return new(false, "document.format.unsupported", "application/octet-stream");
        if (!content.CanSeek) return new(false, "document.content.unscannable", contentType);
        content.Position = 0;
        var prefix = new byte[Math.Min(8192, checked((int)Math.Min(content.Length, 8192)))];
        _ = await content.ReadAsync(prefix, cancellationToken);
        content.Position = 0;
        if (prefix.Length == 0) return new(false, "document.empty", contentType);
        var accepted = extension.ToLowerInvariant() switch
        {
            ".pdf" => StartsWith(prefix, "%PDF-"u8),
            ".doc" => IsOle(prefix) && !ContainsUnsafeOfficeMarker(prefix),
            ".docx" => InspectZip(content, true),
            ".odt" => InspectZip(content, false),
            ".rtf" => StartsWith(prefix, "{\\rtf"u8),
            ".txt" => IsBoundedText(prefix),
            ".jpg" or ".jpeg" => prefix.AsSpan().StartsWith(new byte[] { 0xff, 0xd8, 0xff }),
            ".png" => prefix.AsSpan().StartsWith(new byte[] { 0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a }),
            ".tif" or ".tiff" => prefix.AsSpan().StartsWith("II*\0"u8) || prefix.AsSpan().StartsWith("MM\0*"u8),
            ".bmp" => prefix.AsSpan().StartsWith("BM"u8),
            _ => false,
        };
        content.Position = 0;
        return accepted
            ? new(true, "document.accepted", contentType)
            : new(false, "document.content.mismatch_or_unsafe", contentType);
    }

    private static bool InspectZip(Stream content, bool docx)
    {
        try
        {
            using var archive = new ZipArchive(content, ZipArchiveMode.Read, leaveOpen: true);
            if (archive.Entries.Count is 0 or > 500) return false;
            var names = archive.Entries.Select(entry => entry.FullName).ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (names.Any(name => name.Contains("vbaProject.bin", StringComparison.OrdinalIgnoreCase) || name.Contains("EncryptedPackage", StringComparison.OrdinalIgnoreCase))) return false;
            if (archive.Entries.Sum(entry => entry.Length) > 100 * 1024 * 1024) return false;
            return docx
                ? names.Contains("[Content_Types].xml") && names.Contains("word/document.xml")
                : archive.GetEntry("mimetype") is { } mime && ReadSmallEntry(mime) == "application/vnd.oasis.opendocument.text";
        }
        catch (InvalidDataException)
        {
            return false;
        }
        finally
        {
            content.Position = 0;
        }
    }

    private static string ReadSmallEntry(ZipArchiveEntry entry)
    {
        if (entry.Length > 200) return string.Empty;
        using var reader = new StreamReader(entry.Open(), Encoding.ASCII, false, leaveOpen: false);
        return reader.ReadToEnd();
    }

    private static bool IsOle(ReadOnlySpan<byte> value) => value.StartsWith(new byte[] { 0xd0, 0xcf, 0x11, 0xe0, 0xa1, 0xb1, 0x1a, 0xe1 });
    private static bool ContainsUnsafeOfficeMarker(byte[] value)
    {
        var text = Encoding.ASCII.GetString(value);
        return text.Contains("EncryptedPackage", StringComparison.OrdinalIgnoreCase) || text.Contains("_VBA_PROJECT", StringComparison.OrdinalIgnoreCase);
    }
    private static bool IsBoundedText(byte[] value) => !value.Contains((byte)0) && Encoding.UTF8.GetString(value).All(character => !char.IsControl(character) || character is '\r' or '\n' or '\t');
    private static bool StartsWith(byte[] value, ReadOnlySpan<byte> prefix) => value.AsSpan().StartsWith(prefix);
}
