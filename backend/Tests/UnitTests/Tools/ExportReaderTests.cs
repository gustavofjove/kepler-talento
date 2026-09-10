using System.Text;
using KeplerTalento.Tools.DataMigration.Export;
using Xunit;

namespace KeplerTalento.Tests.UnitTests.Tools;

public sealed class ExportReaderTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"ktl-export-{Guid.NewGuid():N}");

    public ExportReaderTests()
    {
        Directory.CreateDirectory(_root);
        Directory.CreateDirectory(Path.Combine(_root, ExportContract.FilesDirectory));
        foreach (var file in ExportContract.Files)
        {
            WriteHeaderOnly(file);
        }
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private void WriteHeaderOnly(string file) =>
        File.WriteAllText(
            Path.Combine(_root, file),
            string.Join(',', ExportContract.Columns[file]) + "\n",
            new UTF8Encoding(false));

    private void Write(string file, params string[] rows) =>
        File.WriteAllText(
            Path.Combine(_root, file),
            string.Join(',', ExportContract.Columns[file]) + "\n" + string.Join('\n', rows) + "\n",
            new UTF8Encoding(false));

    private (bool Ok, IReadOnlyList<string> Problems) Read()
    {
        var ok = new ExportReader().TryRead(_root, out _, out var problems);
        return (ok, problems.Select(problem => problem.ToString()).ToList());
    }

    [Fact]
    public void A_complete_header_only_export_set_is_structurally_valid()
    {
        var (ok, problems) = Read();

        Assert.True(ok, string.Join("; ", problems));
    }

    [Fact]
    public void A_missing_file_is_reported_rather_than_assumed_empty()
    {
        File.Delete(Path.Combine(_root, ExportContract.Skills));

        var (ok, problems) = Read();

        Assert.False(ok);
        Assert.Contains(problems, problem => problem.Contains("skills.csv", StringComparison.Ordinal));
    }

    [Fact]
    public void Every_missing_file_is_reported_in_one_pass()
    {
        File.Delete(Path.Combine(_root, ExportContract.Skills));
        File.Delete(Path.Combine(_root, ExportContract.Programs));

        var (_, problems) = Read();

        Assert.Contains(problems, problem => problem.Contains("skills.csv", StringComparison.Ordinal));
        Assert.Contains(problems, problem => problem.Contains("programs.csv", StringComparison.Ordinal));
    }

    [Fact]
    public void A_missing_column_is_reported_by_name()
    {
        File.WriteAllText(Path.Combine(_root, ExportContract.Skills), "SourceKey,CandidateSourceKey,Skill\n");

        var (ok, problems) = Read();

        Assert.False(ok);
        Assert.Contains(problems, problem => problem.Contains("missing required columns: Level, Notes", StringComparison.Ordinal));
    }

    [Fact]
    public void A_renamed_column_shows_up_as_both_missing_and_unexpected()
    {
        // The pair is what makes a rename obvious instead of mysterious.
        File.WriteAllText(Path.Combine(_root, ExportContract.Skills), "SourceKey,CandidateSourceKey,Skill,Nivel,Notes\n");

        var (_, problems) = Read();

        Assert.Contains(problems, problem => problem.Contains("missing required columns: Level", StringComparison.Ordinal));
        Assert.Contains(problems, problem => problem.Contains("unexpected columns: Nivel", StringComparison.Ordinal));
    }

    [Fact]
    public void A_file_that_is_not_valid_utf8_is_rejected_rather_than_silently_mangled()
    {
        File.WriteAllBytes(
            Path.Combine(_root, ExportContract.Skills),
            [.. "SourceKey,CandidateSourceKey,Skill,Level,Notes\nS-1,C-1,"u8.ToArray(), 0xC3, 0x28, .. ",Alto,\n"u8.ToArray()]);

        var (ok, problems) = Read();

        Assert.False(ok);
        Assert.Contains(problems, problem => problem.Contains("not valid UTF-8", StringComparison.Ordinal));
    }

    [Fact]
    public void A_row_without_a_source_key_is_reported()
    {
        Write(ExportContract.Skills, ",C-1,Análisis,Alto,");

        var (ok, problems) = Read();

        Assert.False(ok);
        Assert.Contains(problems, problem => problem.Contains("have no SourceKey", StringComparison.Ordinal));
    }

    [Fact]
    public void Duplicate_source_keys_are_named_so_the_operator_can_find_them_in_access()
    {
        Write(ExportContract.Skills, "S-1,C-1,Análisis,Alto,", "S-1,C-2,Compras,Medio,");

        var (ok, problems) = Read();

        Assert.False(ok);
        Assert.Contains(problems, problem => problem.Contains("duplicate SourceKey values: S-1", StringComparison.Ordinal));
    }

    [Fact]
    public void A_manifest_with_rows_but_no_files_directory_is_reported()
    {
        Write(
            ExportContract.Documents,
            "D-1,C-1,cv.txt,CV,cv.txt,text/plain,"
                + new string('a', 64) + ",true");
        Directory.Delete(Path.Combine(_root, ExportContract.FilesDirectory), recursive: true);

        var (ok, problems) = Read();

        Assert.False(ok);
        Assert.Contains(problems, problem => problem.Contains("files directory is absent", StringComparison.Ordinal));
    }

    [Fact]
    public void An_empty_manifest_does_not_require_a_files_directory()
    {
        Directory.Delete(Path.Combine(_root, ExportContract.FilesDirectory), recursive: true);

        var (ok, problems) = Read();

        Assert.True(ok, string.Join("; ", problems));
    }
}
