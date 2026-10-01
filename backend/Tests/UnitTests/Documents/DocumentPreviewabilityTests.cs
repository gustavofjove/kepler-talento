using KeplerTalento.Application.Features.Documents;
using KeplerTalento.Domain.Documents;
using Xunit;

namespace KeplerTalento.Tests.UnitTests.Documents;

/// <summary>
/// KTL-35: the single rule behind «Ver» in the candidate tables. The list queries evaluate the
/// same expression in SQL; these cases pin it to the availability the document API reports, so
/// a table never offers a preview the candidate page would refuse, or the reverse.
/// </summary>
public sealed class DocumentPreviewabilityTests
{
    private const string Pdf = "application/pdf";
    private const string Docx = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

    public static TheoryData<string, string, DocumentScanState, string, string?, bool> Cases => new()
    {
        { "clean pdf", Pdf, DocumentScanState.Clean, new string('a', 64), null, true },
        { "clean pdf imported with its binary", Pdf, DocumentScanState.Clean, new string('a', 64), "legacy-1", true },
        { "clean docx", Docx, DocumentScanState.Clean, new string('a', 64), null, false },
        { "pending pdf", Pdf, DocumentScanState.PendingScan, new string('a', 64), null, false },
        { "infected pdf", Pdf, DocumentScanState.Infected, new string('a', 64), null, false },
        { "rejected pdf", Pdf, DocumentScanState.Rejected, new string('a', 64), null, false },
        { "unscannable pdf", Pdf, DocumentScanState.ScanFailed, new string('a', 64), null, false },
        { "legacy pdf without a binary", Pdf, DocumentScanState.Clean, string.Empty, "legacy-2", false },
        { "legacy pdf with a blank hash", Pdf, DocumentScanState.Clean, "   ", "legacy-3", false },
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public void A_document_is_previewable_only_as_a_clean_pdf_with_its_binary(
        string description, string contentType, DocumentScanState state, string sha256, string? sourceKey, bool expected)
    {
        var document = Build(contentType, state, sha256, sourceKey);

        Assert.True(expected == document.CanBePreviewed, description);
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Previewability_matches_the_available_pdf_the_document_api_reports(
        string description, string contentType, DocumentScanState state, string sha256, string? sourceKey, bool expected)
    {
        var response = CandidateDocumentResponse.From(Build(contentType, state, sha256, sourceKey));

        var reportedPreviewable = response.AvailabilityState == "Available" && response.MimeType == Pdf;

        Assert.True(expected == reportedPreviewable, description);
    }

    public static TheoryData<string, string, DocumentScanState, string, string?, bool> DownloadCases => new()
    {
        { "clean pdf", Pdf, DocumentScanState.Clean, new string('a', 64), null, true },
        { "clean docx", Docx, DocumentScanState.Clean, new string('a', 64), null, true },
        { "clean docx imported with its binary", Docx, DocumentScanState.Clean, new string('a', 64), "legacy-4", true },
        { "pending docx", Docx, DocumentScanState.PendingScan, new string('a', 64), null, false },
        { "infected pdf", Pdf, DocumentScanState.Infected, new string('a', 64), null, false },
        { "rejected docx", Docx, DocumentScanState.Rejected, new string('a', 64), null, false },
        { "unscannable pdf", Pdf, DocumentScanState.ScanFailed, new string('a', 64), null, false },
        { "legacy docx without a binary", Docx, DocumentScanState.Clean, string.Empty, "legacy-5", false },
    };

    [Theory]
    [MemberData(nameof(DownloadCases))]
    public void A_document_is_downloadable_when_clean_with_its_binary_in_any_format(
        string description, string contentType, DocumentScanState state, string sha256, string? sourceKey, bool expected)
    {
        var document = Build(contentType, state, sha256, sourceKey);

        Assert.True(expected == document.CanBeDownloaded, description);
        Assert.True(expected == (CandidateDocumentResponse.From(document).AvailabilityState == "Available"), description);
    }

    private static CandidateDocument Build(string contentType, DocumentScanState state, string sha256, string? sourceKey)
    {
        var now = DateTimeOffset.UtcNow;
        var document = new CandidateDocument(Guid.CreateVersion7(), Guid.CreateVersion7(), "opaque/key.bin", "cv", contentType, 1024, sha256, now);
        document.SetSourceKey(sourceKey);
        switch (state)
        {
            case DocumentScanState.Clean:
                document.MarkClean("test", now);
                break;
            case DocumentScanState.PendingScan:
                break;
            default:
                document.MarkUnavailable(state, "test.refused", now);
                break;
        }
        return document;
    }
}
