using System.Runtime.CompilerServices;
using System.Text;
using KeplerTalento.Application.Abstractions.Import;
using KeplerTalento.Application.Import;
using KeplerTalento.Domain.Import;

namespace KeplerTalento.Infrastructure.Import;

/// <summary>
/// A streaming RFC 4180 reader for the candidate import file (design D8, D9).
/// </summary>
/// <remarks>
/// <para>
/// The file is never buffered whole. Bytes are counted as they are read and the read stops the
/// moment the byte limit is crossed; rows are yielded one at a time and the read stops the moment
/// the row limit is crossed. A single record is bounded as well, so a file that is one enormous
/// quoted field cannot make the reader hold it in memory.
/// </para>
/// <para>
/// The delimiter is a comma, or a semicolon when the header uses semicolons and no commas — which
/// is what a spreadsheet saved as CSV in a Spanish locale produces. Blank lines are ignored and do
/// not count as rows. Row numbers are 1-based and count data rows only, the header excluded.
/// </para>
/// <para>
/// No exception this raises carries a value from a data row. Header problems name the column,
/// which is file structure, bounded and stripped of control characters.
/// </para>
/// </remarks>
public sealed class CsvImportRowReader : IImportRowReader
{
    private const int MaximumRecordCharacters = 64 * 1024;
    private const int MaximumColumnNameLength = 64;
    private const char ByteOrderMark = (char)0xFEFF;

    public string ContentType => CandidateImportContract.AcceptedContentType;

    public async IAsyncEnumerable<ImportFileRow> ReadAsync(
        Stream content,
        IReadOnlyCollection<string> requiredColumns,
        IReadOnlyCollection<string> knownColumns,
        ImportLimits limits,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await using var bounded = new ByteLimitedStream(content, limits.MaximumBytes);
        using var reader = new StreamReader(
            bounded,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true),
            detectEncodingFromByteOrderMarks: false,
            bufferSize: 16 * 1024,
            leaveOpen: true);

        var records = new CsvRecordReader(reader);
        var header = await ReadOrThrowAsync(records, delimiter: null, cancellationToken);
        while (header is not null && IsBlank(header))
        {
            header = await ReadOrThrowAsync(records, delimiter: null, cancellationToken);
        }
        if (header is null)
        {
            throw new ImportStructuralException(ImportReasonCodes.HeaderMissing);
        }

        var columns = NormalizeHeader(header, requiredColumns, knownColumns);
        var rowNumber = 0;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var record = await ReadOrThrowAsync(records, records.Delimiter, cancellationToken);
            if (record is null)
            {
                yield break;
            }
            if (IsBlank(record))
            {
                continue;
            }
            rowNumber++;
            if (rowNumber > limits.MaximumRows)
            {
                throw new ImportStructuralException(ImportReasonCodes.RowLimitExceeded, limits.MaximumRows.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }
            if (record.Count != columns.Count)
            {
                yield return new ImportFileRow(rowNumber, new Dictionary<string, string>(StringComparer.Ordinal), shapeValid: false);
                continue;
            }
            var values = new Dictionary<string, string>(columns.Count, StringComparer.Ordinal);
            for (var index = 0; index < columns.Count; index++)
            {
                values[columns[index]] = record[index];
            }
            yield return new ImportFileRow(rowNumber, values, shapeValid: true);
        }
    }

    private static async Task<List<string>?> ReadOrThrowAsync(CsvRecordReader records, char? delimiter, CancellationToken cancellationToken)
    {
        try
        {
            return await records.ReadAsync(delimiter, cancellationToken);
        }
        catch (DecoderFallbackException)
        {
            throw new ImportStructuralException(ImportReasonCodes.FileEncodingInvalid);
        }
        catch (ByteLimitExceededException)
        {
            throw new ImportStructuralException(ImportReasonCodes.FileTooLarge);
        }
        catch (MalformedCsvException)
        {
            throw new ImportStructuralException(ImportReasonCodes.FileMalformed);
        }
    }

    private static List<string> NormalizeHeader(
        List<string> header,
        IReadOnlyCollection<string> requiredColumns,
        IReadOnlyCollection<string> knownColumns)
    {
        var columns = new List<string>(header.Count);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var raw in header)
        {
            var name = raw.Trim().TrimStart(ByteOrderMark).Trim().ToLowerInvariant();
            if (!knownColumns.Contains(name))
            {
                throw new ImportStructuralException(ImportReasonCodes.ColumnUnknown, SafeColumnName(name));
            }
            if (!seen.Add(name))
            {
                throw new ImportStructuralException(ImportReasonCodes.ColumnDuplicate, name);
            }
            columns.Add(name);
        }
        foreach (var required in requiredColumns)
        {
            if (!seen.Contains(required))
            {
                throw new ImportStructuralException(ImportReasonCodes.ColumnMissing, required);
            }
        }
        return columns;
    }

    /// <summary>
    /// An unknown column name is caller text that is not in the contract. It is file structure,
    /// not row data, but it is still bounded and stripped of anything unprintable before it is
    /// stored as problem detail.
    /// </summary>
    private static string SafeColumnName(string name)
    {
        var printable = new string([.. name.Where(character => !char.IsControl(character))]);
        return printable.Length > MaximumColumnNameLength ? printable[..MaximumColumnNameLength] : printable;
    }

    private static bool IsBlank(List<string> record) => record.Count == 1 && string.IsNullOrWhiteSpace(record[0]);

    private sealed class MalformedCsvException : Exception;

    private sealed class ByteLimitExceededException : IOException;

    /// <summary>Record-at-a-time RFC 4180 parsing over a character stream.</summary>
    private sealed class CsvRecordReader(TextReader reader)
    {
        private readonly char[] _buffer = new char[16 * 1024];
        private int _position;
        private int _length;

        public char Delimiter { get; private set; } = ',';

        public async Task<List<string>?> ReadAsync(char? delimiter, CancellationToken cancellationToken)
        {
            if (delimiter is null)
            {
                // The header decides the delimiter. It is read as raw text up to the first line break
                // outside quotes, and then parsed like any other record.
                var line = (await ReadRawRecordAsync(cancellationToken))?.TrimStart(ByteOrderMark);
                if (line is null)
                {
                    return null;
                }
                Delimiter = CountOutsideQuotes(line, ';') > 0 && CountOutsideQuotes(line, ',') == 0 ? ';' : ',';
                return Parse(line, Delimiter);
            }
            var raw = await ReadRawRecordAsync(cancellationToken);
            return raw is null ? null : Parse(raw, delimiter.Value);
        }

        private async Task<string?> ReadRawRecordAsync(CancellationToken cancellationToken)
        {
            var text = new StringBuilder();
            var inQuotes = false;
            while (true)
            {
                if (_position == _length)
                {
                    _length = await reader.ReadAsync(_buffer.AsMemory(), cancellationToken);
                    _position = 0;
                    if (_length == 0)
                    {
                        if (inQuotes)
                        {
                            throw new MalformedCsvException();
                        }
                        return text.Length == 0 ? null : text.ToString();
                    }
                }
                var character = _buffer[_position++];
                if (character == '"')
                {
                    inQuotes = !inQuotes;
                }
                else if (!inQuotes && character is '\n' or '\r')
                {
                    if (character == '\r')
                    {
                        await SkipLineFeedAsync(cancellationToken);
                    }
                    return text.ToString();
                }
                text.Append(character);
                if (text.Length > MaximumRecordCharacters)
                {
                    throw new MalformedCsvException();
                }
            }
        }

        private async Task SkipLineFeedAsync(CancellationToken cancellationToken)
        {
            if (_position == _length)
            {
                _length = await reader.ReadAsync(_buffer.AsMemory(), cancellationToken);
                _position = 0;
            }
            if (_position < _length && _buffer[_position] == '\n')
            {
                _position++;
            }
        }

        private static int CountOutsideQuotes(string line, char target)
        {
            var count = 0;
            var inQuotes = false;
            foreach (var character in line)
            {
                if (character == '"')
                {
                    inQuotes = !inQuotes;
                }
                else if (!inQuotes && character == target)
                {
                    count++;
                }
            }
            return count;
        }

        private static List<string> Parse(string record, char delimiter)
        {
            var fields = new List<string>();
            var field = new StringBuilder();
            var inQuotes = false;
            var quotedField = false;
            for (var index = 0; index < record.Length; index++)
            {
                var character = record[index];
                if (inQuotes)
                {
                    if (character == '"')
                    {
                        if (index + 1 < record.Length && record[index + 1] == '"')
                        {
                            field.Append('"');
                            index++;
                        }
                        else
                        {
                            inQuotes = false;
                        }
                    }
                    else
                    {
                        field.Append(character);
                    }
                    continue;
                }
                if (character == '"')
                {
                    // A quote opens a field only at its start; anywhere else the record is not CSV.
                    if (field.Length > 0 || quotedField)
                    {
                        throw new MalformedCsvException();
                    }
                    inQuotes = true;
                    quotedField = true;
                }
                else if (character == delimiter)
                {
                    fields.Add(field.ToString());
                    field.Clear();
                    quotedField = false;
                }
                else
                {
                    if (quotedField)
                    {
                        throw new MalformedCsvException();
                    }
                    field.Append(character);
                }
            }
            if (inQuotes)
            {
                throw new MalformedCsvException();
            }
            fields.Add(field.ToString());
            return fields;
        }
    }

    /// <summary>Counts bytes as they are read and stops at the limit, instead of after the fact.</summary>
    private sealed class ByteLimitedStream(Stream inner, long maximumBytes) : Stream
    {
        private long _read;

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => _read; set => throw new NotSupportedException(); }

        public override int Read(byte[] buffer, int offset, int count) =>
            Count(inner.Read(buffer, offset, count));

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            Count(await inner.ReadAsync(buffer, cancellationToken));

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

        private int Count(int read)
        {
            _read += read;
            if (_read > maximumBytes)
            {
                throw new ByteLimitExceededException();
            }
            return read;
        }

        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        // The inner stream belongs to the caller.
        protected override void Dispose(bool disposing) { }
        public override ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
