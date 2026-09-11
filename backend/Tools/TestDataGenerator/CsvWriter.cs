using System.Buffers;
using System.Text;

namespace KeplerTalento.Tools.TestDataGenerator;

/// <summary>
/// Writes the RFC 4180 subset <c>CsvDocument</c> parses: comma separated, CRLF terminated,
/// UTF-8 without a byte-order mark, a field quoted only when it holds a comma, a quote or a
/// newline, and an embedded quote doubled.
/// </summary>
public sealed class CsvWriter(IReadOnlyList<string> header)
{
    private readonly StringBuilder _builder = new();
    private bool _headerWritten;

    public void WriteRow(params string?[] fields)
    {
        if (!_headerWritten)
        {
            AppendRecord(header);
            _headerWritten = true;
        }

        if (fields.Length != header.Count)
        {
            throw new ArgumentException(
                $"Expected {header.Count} fields to match the header, got {fields.Length}.",
                nameof(fields));
        }
        AppendRecord(fields);
    }

    private void AppendRecord(IReadOnlyList<string?> fields)
    {
        for (var index = 0; index < fields.Count; index++)
        {
            if (index > 0)
            {
                _builder.Append(',');
            }
            _builder.Append(Escape(fields[index]));
        }
        _builder.Append("\r\n");
    }

    private static readonly SearchValues<char> MustQuote = SearchValues.Create(",\"\r\n");

    private static string Escape(string? value)
    {
        var text = value ?? string.Empty;
        if (!text.AsSpan().ContainsAny(MustQuote))
        {
            return text;
        }
        return $"\"{text.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
    }

    public async Task SaveAsync(string path, CancellationToken cancellationToken)
    {
        if (!_headerWritten)
        {
            AppendRecord(header);
            _headerWritten = true;
        }
        // UTF8Encoding(false) rather than Encoding.UTF8: the latter emits a preamble.
        await File.WriteAllTextAsync(path, _builder.ToString(), new UTF8Encoding(false), cancellationToken);
    }
}
