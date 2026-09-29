using KeplerTalento.Application.Abstractions.CvExtraction;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace KeplerTalento.Infrastructure.CvExtraction;

/// <summary>
/// Reads PDF text as lines, top to bottom, with the point size of each line (KTL-32 design D5).
/// </summary>
/// <remarks>
/// Words are grouped into a line by baseline, and a line is split again where the horizontal gap
/// between two words is wide, so a two-column layout does not fuse its sidebar into the body.
/// Only the first <see cref="CvReadBounds.MaximumPages"/> pages are opened. Parser exceptions are
/// replaced with <see cref="CvUnreadableException"/>: their messages can quote document content.
/// </remarks>
public sealed class PdfCvTextReader
{
    private const double BaselineTolerance = 2.0;
    /// <summary>A gap wider than this many average character widths starts a new column.</summary>
    private const double ColumnGapCharacters = 6.0;

    public CvText Read(Stream content, CvReadBounds bounds, CancellationToken cancellationToken)
    {
        try
        {
            using var document = PdfDocument.Open(content, new ParsingOptions { UseLenientParsing = false });
            var lines = new List<CvLine>();
            var characters = 0;
            var pages = Math.Min(document.NumberOfPages, bounds.MaximumPages);
            for (var number = 1; number <= pages && characters < bounds.MaximumCharacters; number++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var page = document.GetPage(number);
                foreach (var line in Lines(page))
                {
                    var text = line.Text.Length + characters > bounds.MaximumCharacters
                        ? line.Text[..Math.Max(0, bounds.MaximumCharacters - characters)]
                        : line.Text;
                    if (text.Length == 0)
                    {
                        break;
                    }
                    lines.Add(line with { Page = number - 1, Text = text });
                    characters += text.Length;
                }
            }
            return new CvText(lines, pages);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            throw new CvUnreadableException();
        }
    }

    private static IEnumerable<CvLine> Lines(Page page)
    {
        var words = page.GetWords()
            .Where(word => !string.IsNullOrWhiteSpace(word.Text))
            .OrderByDescending(word => word.BoundingBox.Bottom)
            .ThenBy(word => word.BoundingBox.Left)
            .ToList();
        var rows = new List<List<Word>>();
        foreach (var word in words)
        {
            var row = rows.LastOrDefault();
            if (row is not null && Math.Abs(row[0].BoundingBox.Bottom - word.BoundingBox.Bottom) <= BaselineTolerance)
            {
                row.Add(word);
            }
            else
            {
                rows.Add([word]);
            }
        }

        foreach (var row in rows)
        {
            var ordered = row.OrderBy(word => word.BoundingBox.Left).ToList();
            var segment = new List<Word> { ordered[0] };
            for (var index = 1; index < ordered.Count; index++)
            {
                var previous = ordered[index - 1];
                var current = ordered[index];
                var averageWidth = previous.BoundingBox.Width / Math.Max(1, previous.Text.Length);
                if (current.BoundingBox.Left - previous.BoundingBox.Right > ColumnGapCharacters * Math.Max(averageWidth, 1.0))
                {
                    yield return ToLine(segment);
                    segment = [];
                }
                segment.Add(current);
            }
            yield return ToLine(segment);
        }
    }

    private static CvLine ToLine(List<Word> words)
    {
        var size = words.SelectMany(word => word.Letters).Select(letter => letter.PointSize).DefaultIfEmpty(0).Max();
        return new CvLine(0, string.Join(' ', words.Select(word => word.Text)), size > 0 ? size : null);
    }
}
