using System.Text;
using KeplerTalento.Application.Abstractions.Import;
using KeplerTalento.Domain.Import;

namespace KeplerTalento.Infrastructure.Import;

/// <summary>
/// Content sniffing for a declared CSV file (design D6). Runs on the clean, promoted file and
/// before the parser sees a byte of it.
/// </summary>
/// <remarks>
/// A CSV has no magic number, so this works by refusal: the known binary containers are refused
/// by signature — a ".csv" that is really a zip (xlsx, docx), an OLE compound file (xls, doc, with
/// or without macros), a PDF or an executable — and anything else must look like UTF-8 text
/// without NUL bytes or control characters other than tab and line breaks. UTF-16 is refused too:
/// the contract is UTF-8.
/// </remarks>
public sealed class ImportFileInspector : IImportFileInspector
{
    private const int PrefixBytes = 64 * 1024;

    private static readonly byte[][] BinarySignatures =
    [
        [0x50, 0x4b, 0x03, 0x04], // zip: xlsx, docx, ods
        [0x50, 0x4b, 0x05, 0x06], // empty zip
        [0xd0, 0xcf, 0x11, 0xe0, 0xa1, 0xb1, 0x1a, 0xe1], // OLE: xls, doc
        "%PDF-"u8.ToArray(),
        "MZ"u8.ToArray(),
        [0x7f, 0x45, 0x4c, 0x46], // ELF
        [0x1f, 0x8b], // gzip
        "{\\rtf"u8.ToArray(),
        [0xff, 0xfe], // UTF-16 LE BOM
        [0xfe, 0xff], // UTF-16 BE BOM
    ];

    public async Task<ImportFileInspection> InspectAsync(Stream content, CancellationToken cancellationToken)
    {
        var buffer = new byte[PrefixBytes];
        var length = 0;
        int read;
        while (length < buffer.Length && (read = await content.ReadAsync(buffer.AsMemory(length), cancellationToken)) > 0)
        {
            length += read;
        }
        var prefix = buffer.AsSpan(0, length);
        if (length == 0)
        {
            return new(false, ImportReasonCodes.FileEmpty);
        }
        foreach (var signature in BinarySignatures)
        {
            if (prefix.StartsWith(signature))
            {
                return new(false, ImportReasonCodes.FileContentMismatch);
            }
        }
        if (prefix.Contains((byte)0))
        {
            return new(false, ImportReasonCodes.FileContentMismatch);
        }

        // The prefix may end partway through a multi-byte character; only a sequence that is
        // invalid before the final few bytes counts against the file.
        var decoder = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true).GetDecoder();
        var characters = new char[length + 1];
        int decoded;
        try
        {
            decoded = decoder.GetChars(buffer, 0, length, characters, 0, flush: false);
        }
        catch (DecoderFallbackException)
        {
            return new(false, ImportReasonCodes.FileEncodingInvalid);
        }
        foreach (var character in characters.AsSpan(0, decoded))
        {
            if (char.IsControl(character) && character is not ('\t' or '\r' or '\n'))
            {
                return new(false, ImportReasonCodes.FileContentMismatch);
            }
        }
        return new(true, "import.file.accepted");
    }
}
