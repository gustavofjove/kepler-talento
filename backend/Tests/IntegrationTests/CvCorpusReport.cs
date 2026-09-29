using System.Globalization;
using System.Text;
using System.Text.Json;
using KeplerTalento.Application.Abstractions.CvExtraction;
using KeplerTalento.Infrastructure.CvExtraction;
using Xunit;
using Xunit.Abstractions;

namespace KeplerTalento.Tests.IntegrationTests;

/// <summary>
/// KTL-32 reliability measurement: runs the real readers and rules over a local corpus of CVs and
/// reports, per field, how often the suggestion was right, wrong or missing.
/// </summary>
/// <remarks>
/// The corpus lives in <c>backend/Tests/CvCorpus/</c>, which git ignores: sample CVs and their
/// expected values are personal data and never leave the operator's machine. Without that folder
/// the test does nothing, so CI never needs it. The report carries counts and file positions only,
/// never a value. See <c>docs/ktl-32/extraction-test-sheet.md</c>.
/// </remarks>
[Trait("Category", "CvCorpus")]
public sealed class CvCorpusReport(ITestOutputHelper output)
{
    private static readonly string[] Fields = ["firstName", "lastName", "email", "phone", "location", "province"];

    [Fact]
    public async Task Report_extraction_reliability_over_the_local_corpus()
    {
        var corpus = CorpusDirectory();
        var expectedFile = corpus is null ? null : Path.Combine(corpus, "expected.json");
        if (expectedFile is null || !File.Exists(expectedFile))
        {
            output.WriteLine("No local CV corpus (backend/Tests/CvCorpus/expected.json); nothing to measure.");
            return;
        }

        var expectations = JsonSerializer.Deserialize<List<Dictionary<string, string?>>>(
            await File.ReadAllTextAsync(expectedFile),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];
        var reader = new CvTextReader(new PdfCvTextReader(), new DocxCvTextReader());
        var extractor = new RuleBasedCandidateDraftExtractor(new SpanishPlaces());
        var bounds = new CvDraftOptions().Bounds;
        var tally = Fields.ToDictionary(field => field, _ => new FieldTally());
        int files = 0, noText = 0, unreadable = 0;

        foreach (var expected in expectations)
        {
            var name = expected.GetValueOrDefault("file");
            var path = name is null ? null : Path.Combine(corpus!, name);
            var kind = Path.GetExtension(name ?? string.Empty).ToLowerInvariant() switch
            {
                ".pdf" => CvFileKind.Pdf,
                ".docx" => CvFileKind.Docx,
                _ => (CvFileKind?)null,
            };
            if (path is null || kind is null || !File.Exists(path))
            {
                continue;
            }
            files++;
            CvText text;
            try
            {
                await using var content = new MemoryStream(await File.ReadAllBytesAsync(path));
                text = await reader.ReadAsync(kind.Value, content, bounds, CancellationToken.None);
            }
            catch (CvUnreadableException)
            {
                unreadable++;
                continue;
            }
            if (!text.HasText)
            {
                noText++;
                continue;
            }
            var draft = extractor.Extract(text);
            foreach (var field in Fields)
            {
                tally[field].Add(expected.GetValueOrDefault(field), Suggestion(draft, field), field);
            }
        }

        var report = Render(files, noText, unreadable, tally);
        output.WriteLine(report);
        await File.WriteAllTextAsync(Path.Combine(corpus!, "report.md"), report);
    }

    private static FieldSuggestion? Suggestion(CandidateDraftSuggestions draft, string field) => field switch
    {
        "firstName" => draft.FirstName,
        "lastName" => draft.LastName,
        "email" => draft.Email,
        "phone" => draft.Phone,
        "location" => draft.Location,
        _ => draft.Province,
    };

    private static string Render(int files, int noText, int unreadable, Dictionary<string, FieldTally> tally)
    {
        var builder = new StringBuilder();
        builder.AppendLine(CultureInfo.InvariantCulture, $"# KTL-32 CV corpus report ({DateTimeOffset.UtcNow:yyyy-MM-dd})");
        builder.AppendLine();
        builder.AppendLine(CultureInfo.InvariantCulture, $"Files read: {files}; no text layer: {noText}; unreadable: {unreadable}.");
        builder.AppendLine();
        builder.AppendLine("| Field | Expected | Correct | Wrong | Missing | Spurious | High correct / wrong | Low correct / wrong |");
        builder.AppendLine("| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |");
        foreach (var (field, value) in tally)
        {
            builder.AppendLine(CultureInfo.InvariantCulture,
                $"| {field} | {value.Expected} | {value.Correct} | {value.Wrong} | {value.Missing} | {value.Spurious} | {value.HighCorrect} / {value.HighWrong} | {value.LowCorrect} / {value.LowWrong} |");
        }
        return builder.ToString();
    }

    private static string? CorpusDirectory()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "backend", "KeplerTalento.slnx")))
            {
                return Path.Combine(directory.FullName, "backend", "Tests", "CvCorpus");
            }
        }
        return null;
    }

    /// <summary>Counts only; the values compared never leave this method.</summary>
    private sealed class FieldTally
    {
        public int Expected { get; private set; }
        public int Correct { get; private set; }
        public int Wrong { get; private set; }
        public int Missing { get; private set; }
        public int Spurious { get; private set; }
        public int HighCorrect { get; private set; }
        public int HighWrong { get; private set; }
        public int LowCorrect { get; private set; }
        public int LowWrong { get; private set; }

        public void Add(string? expected, FieldSuggestion? suggested, string field)
        {
            var hasExpected = !string.IsNullOrWhiteSpace(expected);
            if (hasExpected)
            {
                Expected++;
            }
            if (suggested is null)
            {
                if (hasExpected)
                {
                    Missing++;
                }
                return;
            }
            if (!hasExpected)
            {
                Spurious++;
                return;
            }
            var correct = Comparable(expected!, field) == Comparable(suggested.Value, field);
            var high = suggested.Confidence == SuggestionConfidence.High;
            if (correct)
            {
                Correct++;
                if (high) HighCorrect++; else LowCorrect++;
            }
            else
            {
                Wrong++;
                if (high) HighWrong++; else LowWrong++;
            }
        }

        private static string Comparable(string value, string field)
        {
            if (field == "phone")
            {
                var digits = new string(value.Where(char.IsDigit).ToArray());
                return digits.StartsWith("34", StringComparison.Ordinal) && digits.Length == 11 ? digits[2..] : digits;
            }
            var decomposed = value.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
            var letters = decomposed.Where(character =>
                CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark);
            return string.Join(' ', new string(letters.ToArray()).Split([' ', '-'], StringSplitOptions.RemoveEmptyEntries));
        }
    }
}
