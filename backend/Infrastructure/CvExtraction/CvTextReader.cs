using KeplerTalento.Application.Abstractions.CvExtraction;

namespace KeplerTalento.Infrastructure.CvExtraction;

/// <summary>
/// Dispatches to the reader for the format, enforces the time budget from outside the parser, and
/// bounds the parser work actually running.
/// </summary>
/// <remarks>
/// <para>
/// The parsers are synchronous, so they run on the thread pool and the wait is abandoned when the
/// budget expires. A parser blocked inside a single crafted page cannot be interrupted, though, so
/// abandoning the wait does not stop the work.
/// </para>
/// <para>
/// That is why the parser slots live here rather than only at the endpoint: a worker holds its slot
/// until it has really finished, not until its request has returned. Timed-out workers therefore
/// still count against <see cref="CvDraftOptions.MaxConcurrent"/>, and a new read is refused as busy
/// while they are all taken, so repeated submissions cannot pile up parser threads beyond the
/// bound (KTL-32 code review).
/// </para>
/// </remarks>
public sealed class CvTextReader : ICvTextReader, IDisposable
{
    private readonly Func<CvFileKind, Stream, CvReadBounds, CancellationToken, CvText> _parse;
    private readonly SemaphoreSlim _parsers;

    public CvTextReader(PdfCvTextReader pdf, DocxCvTextReader docx, CvDraftOptions options)
        : this(
            (kind, content, bounds, cancellationToken) => kind switch
            {
                CvFileKind.Pdf => pdf.Read(content, bounds, cancellationToken),
                CvFileKind.Docx => docx.Read(content, bounds, cancellationToken),
                _ => throw new CvUnreadableException(),
            },
            options.MaxConcurrent)
    {
    }

    /// <summary>For tests: any parse function, with its own slot count.</summary>
    public CvTextReader(Func<CvFileKind, Stream, CvReadBounds, CancellationToken, CvText> parse, int maxConcurrent)
    {
        _parse = parse;
        _parsers = new SemaphoreSlim(maxConcurrent, maxConcurrent);
    }

    public Task<CvText> ReadAsync(
        CvFileKind kind,
        Stream content,
        CvReadBounds bounds,
        CancellationToken cancellationToken)
    {
        if (!_parsers.Wait(0))
        {
            throw new CvReaderBusyException();
        }
        // No token on Task.Run itself: a worker that never started would never release its slot.
        var read = Task.Run(() =>
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                return _parse(kind, content, bounds, cancellationToken);
            }
            finally
            {
                _parsers.Release();
            }
        });
        return read.WaitAsync(cancellationToken);
    }

    public void Dispose() => _parsers.Dispose();
}
