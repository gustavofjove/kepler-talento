using KeplerTalento.Application.Abstractions.CvExtraction;

namespace KeplerTalento.Infrastructure.CvExtraction;

/// <summary>
/// Dispatches to the reader for the format and enforces the time budget from outside the parser.
/// </summary>
/// <remarks>
/// The parsers are synchronous, so they run on the thread pool and the wait is abandoned when the
/// budget expires. The parser itself also checks the token between pages and paragraphs, so an
/// abandoned read stops soon after instead of running on.
/// </remarks>
public sealed class CvTextReader(PdfCvTextReader pdf, DocxCvTextReader docx) : ICvTextReader
{
    public Task<CvText> ReadAsync(
        CvFileKind kind,
        Stream content,
        CvReadBounds bounds,
        CancellationToken cancellationToken)
    {
        var read = Task.Run(
            () => kind switch
            {
                CvFileKind.Pdf => pdf.Read(content, bounds, cancellationToken),
                CvFileKind.Docx => docx.Read(content, bounds, cancellationToken),
                _ => throw new CvUnreadableException(),
            },
            cancellationToken);
        return read.WaitAsync(cancellationToken);
    }
}
