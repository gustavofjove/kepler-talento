using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml;
using KeplerTalento.Application.Abstractions.CvExtraction;

namespace KeplerTalento.Infrastructure.CvExtraction;

/// <summary>
/// Reads DOCX text as one line per paragraph, headers first, with the framework's
/// <see cref="ZipArchive"/> and <see cref="XmlReader"/> (KTL-32 design D7: no Open XML SDK).
/// </summary>
/// <remarks>
/// Headers come first because many CV templates put the name and contact details there. The run
/// size (<c>w:sz</c>, in half-points) is kept as the line's point size when present. DTDs are
/// prohibited and the characters read are capped, so a crafted part cannot expand without limit.
/// </remarks>
public sealed class DocxCvTextReader
{
    private const string WordNamespace = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    private const long MaximumPartCharacters = 10 * 1024 * 1024;

    public CvText Read(Stream content, CvReadBounds bounds, CancellationToken cancellationToken)
    {
        try
        {
            using var archive = new ZipArchive(content, ZipArchiveMode.Read, leaveOpen: true);
            var parts = archive.Entries
                .Where(entry => entry.FullName.StartsWith("word/header", StringComparison.OrdinalIgnoreCase)
                    && entry.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                .OrderBy(entry => entry.FullName, StringComparer.OrdinalIgnoreCase)
                .ToList();
            var body = archive.GetEntry("word/document.xml") ?? throw new CvUnreadableException();
            parts.Add(body);

            var lines = new List<CvLine>();
            var characters = 0;
            foreach (var part in parts)
            {
                cancellationToken.ThrowIfCancellationRequested();
                using var stream = part.Open();
                characters = ReadPart(stream, lines, characters, bounds.MaximumCharacters, cancellationToken);
                if (characters >= bounds.MaximumCharacters)
                {
                    break;
                }
            }
            return new CvText(lines, 1);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (CvUnreadableException)
        {
            throw;
        }
        catch (Exception)
        {
            throw new CvUnreadableException();
        }
    }

    private static int ReadPart(
        Stream stream,
        List<CvLine> lines,
        int characters,
        int maximumCharacters,
        CancellationToken cancellationToken)
    {
        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersInDocument = MaximumPartCharacters,
            IgnoreComments = true,
            IgnoreProcessingInstructions = true,
        };
        using var reader = XmlReader.Create(stream, settings);
        var paragraph = new StringBuilder();
        double? size = null;

        void Flush()
        {
            var text = paragraph.ToString().Trim();
            paragraph.Clear();
            if (text.Length > 0 && characters < maximumCharacters)
            {
                if (characters + text.Length > maximumCharacters)
                {
                    text = text[..(maximumCharacters - characters)];
                }
                lines.Add(new CvLine(0, text, size));
                characters += text.Length;
            }
            size = null;
        }

        while (reader.Read())
        {
            if (reader.NamespaceURI != WordNamespace)
            {
                continue;
            }
            if (reader.NodeType == XmlNodeType.EndElement && reader.LocalName == "p")
            {
                Flush();
                cancellationToken.ThrowIfCancellationRequested();
                if (characters >= maximumCharacters)
                {
                    break;
                }
                continue;
            }
            if (reader.NodeType != XmlNodeType.Element)
            {
                continue;
            }
            switch (reader.LocalName)
            {
                case "t":
                    paragraph.Append(reader.ReadElementContentAsString());
                    break;
                case "tab":
                    paragraph.Append(' ');
                    break;
                case "br":
                case "cr":
                    Flush();
                    break;
                case "sz":
                    if (double.TryParse(
                            reader.GetAttribute("val", WordNamespace),
                            NumberStyles.Number,
                            CultureInfo.InvariantCulture,
                            out var halfPoints))
                    {
                        size = Math.Max(size ?? 0, halfPoints / 2);
                    }
                    break;
            }
        }
        Flush();
        return characters;
    }
}
