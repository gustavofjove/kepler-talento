using System.Text;
using KeplerTalento.Application.Abstractions.Import;
using KeplerTalento.Application.Import;
using KeplerTalento.Domain.Import;
using KeplerTalento.Infrastructure.Import;
using Xunit;

namespace KeplerTalento.Tests.UnitTests.Import;

public sealed class CsvImportRowReaderTests
{
    private static readonly ImportLimits Limits = new(MaximumRows: 5, MaximumBytes: 4096);

    [Fact]
    public async Task Rows_are_numbered_from_one_after_the_header_ignoring_blank_lines()
    {
        var rows = await ReadAsync("first_name,last_name,email\r\nAna,Ruiz,ana@example.test\r\n\r\nLuis,Gil,luis@example.test\n");

        Assert.Equal([1, 2], rows.Select(row => row.RowNumber));
        Assert.Equal("Luis", rows[1]["first_name"]);
    }

    [Fact]
    public async Task Quoted_fields_keep_delimiters_quotes_and_line_breaks()
    {
        var rows = await ReadAsync("first_name,last_name,email,notes\n\"Ruiz, Ana\",\"O\"\"Neil\",a@example.test,\"line one\nline two\"\n");

        var row = Assert.Single(rows);
        Assert.Equal("Ruiz, Ana", row["first_name"]);
        Assert.Equal("O\"Neil", row["last_name"]);
        Assert.Equal("line one\nline two", row["notes"]);
    }

    [Fact]
    public async Task A_semicolon_header_selects_the_semicolon_delimiter()
    {
        var rows = await ReadAsync("﻿First_Name;Last_Name;Email\nAna;Ruiz;ana@example.test\n");

        Assert.Equal("ana@example.test", Assert.Single(rows)["email"]);
    }

    [Theory]
    [InlineData("first_name,last_name\nAna,Ruiz\n", ImportReasonCodes.ColumnMissing, "email")]
    [InlineData("first_name,last_name,email,emial\n", ImportReasonCodes.ColumnUnknown, "emial")]
    [InlineData("first_name,last_name,email,email\n", ImportReasonCodes.ColumnDuplicate, "email")]
    [InlineData("\n\n", ImportReasonCodes.HeaderMissing, null)]
    [InlineData("first_name,last_name,email\n\"Ana,Ruiz,a@example.test\n", ImportReasonCodes.FileMalformed, null)]
    public async Task A_structural_problem_names_the_column_and_never_a_row_value(string content, string code, string? detail)
    {
        var problem = await Assert.ThrowsAsync<ImportStructuralException>(() => ReadAsync(content));

        Assert.Equal(code, problem.Code);
        Assert.Equal(detail, problem.Detail);
        Assert.DoesNotContain("Ana", problem.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_header_problem_is_raised_before_any_row_is_yielded()
    {
        var yielded = 0;
        await Assert.ThrowsAsync<ImportStructuralException>(async () =>
        {
            await foreach (var _ in new CsvImportRowReader().ReadAsync(
                Stream("first_name,last_name\nAna,Ruiz\n"),
                CandidateImportContract.RequiredColumns,
                CandidateImportContract.KnownColumns,
                Limits,
                CancellationToken.None))
            {
                yielded++;
            }
        });
        Assert.Equal(0, yielded);
    }

    [Fact]
    public async Task Reading_stops_at_the_row_limit_and_names_the_limit()
    {
        var content = new StringBuilder("first_name,last_name,email\n");
        for (var index = 0; index < 50; index++)
        {
            content.Append(System.Globalization.CultureInfo.InvariantCulture, $"A{index},B,a{index}@example.test\n");
        }

        var problem = await Assert.ThrowsAsync<ImportStructuralException>(() => ReadAsync(content.ToString()));

        Assert.Equal(ImportReasonCodes.RowLimitExceeded, problem.Code);
        Assert.Equal("5", problem.Detail);
    }

    [Fact]
    public async Task Reading_stops_at_the_byte_limit_without_buffering_the_file()
    {
        var content = new CountingStream(Encoding.UTF8.GetBytes("first_name,last_name,email,notes\nAna,Ruiz,a@example.test,\"" + new string('x', 1_000_000) + "\"\n"));

        var problem = await Assert.ThrowsAsync<ImportStructuralException>(async () =>
        {
            await foreach (var _ in new CsvImportRowReader().ReadAsync(
                content,
                CandidateImportContract.RequiredColumns,
                CandidateImportContract.KnownColumns,
                Limits,
                CancellationToken.None))
            {
            }
        });

        Assert.Contains(problem.Code, new[] { ImportReasonCodes.FileTooLarge, ImportReasonCodes.FileMalformed });
        Assert.True(content.BytesRead < 200_000, $"Read {content.BytesRead} bytes before stopping.");
    }

    [Fact]
    public async Task Invalid_utf8_is_a_structural_problem()
    {
        var bytes = Encoding.ASCII.GetBytes("first_name,last_name,email\nAna,Ruiz,")
            .Concat(new byte[] { 0xc3, 0x28 })
            .Concat(Encoding.ASCII.GetBytes("\n"))
            .ToArray();

        var problem = await Assert.ThrowsAsync<ImportStructuralException>(() => ReadAsync(new MemoryStream(bytes)));

        Assert.Equal(ImportReasonCodes.FileEncodingInvalid, problem.Code);
    }

    [Fact]
    public async Task A_record_with_the_wrong_field_count_is_yielded_as_a_shape_problem()
    {
        var rows = await ReadAsync("first_name,last_name,email\nAna,Ruiz\n");

        Assert.False(Assert.Single(rows).ShapeValid);
    }

    private static Task<List<ImportFileRow>> ReadAsync(string content) => ReadAsync(Stream(content));

    private static async Task<List<ImportFileRow>> ReadAsync(Stream content)
    {
        var rows = new List<ImportFileRow>();
        await foreach (var row in new CsvImportRowReader().ReadAsync(
            content,
            CandidateImportContract.RequiredColumns,
            CandidateImportContract.KnownColumns,
            Limits,
            CancellationToken.None))
        {
            rows.Add(row);
        }
        return rows;
    }

    private static MemoryStream Stream(string content) => new(Encoding.UTF8.GetBytes(content));

    private sealed class CountingStream(byte[] bytes) : MemoryStream(bytes)
    {
        public long BytesRead { get; private set; }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var read = base.Read(buffer, offset, count);
            BytesRead += read;
            return read;
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            var read = await base.ReadAsync(buffer, cancellationToken);
            BytesRead += read;
            return read;
        }
    }
}
