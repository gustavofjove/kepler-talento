using System.Text;

namespace KeplerTalento.Tools.DataMigration.Export;

/// <summary>
/// One row of an export file, with its position so a structural problem can be located
/// without quoting its contents.
/// </summary>
public sealed class CsvRow(int lineNumber, IReadOnlyDictionary<string, string> values)
{
    public int LineNumber { get; } = lineNumber;

    public string this[string column] => values.TryGetValue(column, out var value) ? value : string.Empty;

    public bool IsEmpty(string column) => string.IsNullOrWhiteSpace(this[column]);
}

/// <summary>
/// A parsed export file: header plus rows keyed by column name.
/// </summary>
/// <remarks>
/// RFC 4180 with the concessions the export contract documents — a byte-order mark is
/// stripped, `LF` and `CRLF` both terminate a record, and a quoted field may contain
/// commas, quotes and newlines, because a candidate's notes routinely do.
/// </remarks>
public sealed class CsvDocument
{
    private CsvDocument(IReadOnlyList<string> header, IReadOnlyList<CsvRow> rows)
    {
        Header = header;
        Rows = rows;
    }

    public IReadOnlyList<string> Header { get; }
    public IReadOnlyList<CsvRow> Rows { get; }

    public static CsvDocument Parse(string text)
    {
        var records = ParseRecords(text);
        if (records.Count == 0)
        {
            return new CsvDocument([], []);
        }

        var header = records[0].Fields.Select(field => field.Trim()).ToArray();
        var rows = new List<CsvRow>(records.Count - 1);
        for (var index = 1; index < records.Count; index++)
        {
            var record = records[index];
            // A trailing newline produces one empty record; that is not a row.
            if (record.Fields.Count == 1 && record.Fields[0].Length == 0)
            {
                continue;
            }
            var values = new Dictionary<string, string>(StringComparer.Ordinal);
            for (var column = 0; column < header.Length; column++)
            {
                values[header[column]] = column < record.Fields.Count ? record.Fields[column] : string.Empty;
            }
            rows.Add(new CsvRow(record.LineNumber, values));
        }
        return new CsvDocument(header, rows);
    }

    private sealed record Record(int LineNumber, IReadOnlyList<string> Fields);

    private static List<Record> ParseRecords(string text)
    {
        if (text.Length > 0 && text[0] == '﻿')
        {
            text = text[1..];
        }

        var records = new List<Record>();
        var fields = new List<string>();
        var field = new StringBuilder();
        var inQuotes = false;
        var lineNumber = 1;
        var recordLineNumber = 1;

        for (var index = 0; index < text.Length; index++)
        {
            var character = text[index];

            if (inQuotes)
            {
                if (character == '"')
                {
                    if (index + 1 < text.Length && text[index + 1] == '"')
                    {
                        field.Append('"');
                        index++;
                        continue;
                    }
                    inQuotes = false;
                    continue;
                }
                if (character == '\n')
                {
                    lineNumber++;
                }
                field.Append(character);
                continue;
            }

            switch (character)
            {
                case '"':
                    inQuotes = true;
                    continue;
                case ',':
                    fields.Add(field.ToString());
                    field.Clear();
                    continue;
                case '\r':
                    continue;
                case '\n':
                    fields.Add(field.ToString());
                    field.Clear();
                    records.Add(new Record(recordLineNumber, fields));
                    fields = [];
                    lineNumber++;
                    recordLineNumber = lineNumber;
                    continue;
                default:
                    field.Append(character);
                    continue;
            }
        }

        if (field.Length > 0 || fields.Count > 0)
        {
            fields.Add(field.ToString());
            records.Add(new Record(recordLineNumber, fields));
        }

        return records;
    }
}
