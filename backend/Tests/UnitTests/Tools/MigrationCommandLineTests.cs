using KeplerTalento.Domain.Operations;
using KeplerTalento.Tools.DataMigration;
using Xunit;

namespace KeplerTalento.Tests.UnitTests.Tools;

public sealed class MigrationCommandLineTests : IDisposable
{
    private readonly string _exportDirectory =
        Path.Combine(Path.GetTempPath(), $"ktl-export-{Guid.NewGuid():N}");

    public MigrationCommandLineTests() => Directory.CreateDirectory(_exportDirectory);

    public void Dispose()
    {
        if (Directory.Exists(_exportDirectory))
        {
            Directory.Delete(_exportDirectory, recursive: true);
        }
    }

    private const string Connection = "Host=localhost;Database=ktl;Username=migrator;Password=x";

    [Fact]
    public void Load_is_refused_without_the_pre_migration_backup_label()
    {
        var parsed = MigrationCommandLine.TryParse(
            [MigrationVerbs.Load, "--connection", Connection, "--export", _exportDirectory],
            out _,
            out var error);

        Assert.False(parsed);
        Assert.Contains("--pre-migration-backup", error, StringComparison.Ordinal);
    }

    [Fact]
    public void Load_is_accepted_with_the_pre_migration_backup_label()
    {
        var parsed = MigrationCommandLine.TryParse(
            [
                MigrationVerbs.Load,
                "--connection", Connection,
                "--export", _exportDirectory,
                "--pre-migration-backup", "pre-ktl7-2026-08-26",
            ],
            out var commandLine,
            out var error);

        Assert.True(parsed, error);
        Assert.Equal("pre-ktl7-2026-08-26", commandLine.PreMigrationBackup);
        Assert.False(commandLine.OverwriteApplicationEdits);
    }

    [Fact]
    public void Overwriting_application_edits_is_off_unless_asked_for()
    {
        MigrationCommandLine.TryParse(
            [
                MigrationVerbs.Load,
                "--connection", Connection,
                "--export", _exportDirectory,
                "--pre-migration-backup", "label",
                "--overwrite-app-edits",
            ],
            out var commandLine,
            out var error);

        Assert.True(commandLine.OverwriteApplicationEdits, error);
    }

    [Fact]
    public void Overwriting_application_edits_is_meaningless_outside_load()
    {
        var parsed = MigrationCommandLine.TryParse(
            [
                MigrationVerbs.Validate,
                "--connection", Connection,
                "--export", _exportDirectory,
                "--overwrite-app-edits",
            ],
            out _,
            out var error);

        Assert.False(parsed);
        Assert.Contains("only applies to 'load'", error, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_needs_an_export_directory_that_exists()
    {
        var parsed = MigrationCommandLine.TryParse(
            [MigrationVerbs.Validate, "--connection", Connection, "--export", Path.Combine(_exportDirectory, "absent")],
            out _,
            out var error);

        Assert.False(parsed);
        Assert.Contains("does not exist", error, StringComparison.Ordinal);
    }

    [Fact]
    public void Report_needs_a_run_identifier()
    {
        Assert.False(MigrationCommandLine.TryParse(
            [MigrationVerbs.Report, "--connection", Connection],
            out _,
            out var error));
        Assert.Contains("--run", error, StringComparison.Ordinal);

        Assert.True(MigrationCommandLine.TryParse(
            [MigrationVerbs.Report, "--connection", Connection, "--run", Guid.NewGuid().ToString()],
            out _,
            out _));
    }

    [Theory]
    [InlineData("sync")]
    [InlineData("delete")]
    public void An_unknown_verb_is_refused(string verb)
    {
        Assert.False(MigrationCommandLine.TryParse([verb, "--connection", Connection], out _, out var error));
        Assert.Contains("Unknown verb", error, StringComparison.Ordinal);
    }

    [Fact]
    public void A_connection_string_is_always_required()
    {
        Assert.False(MigrationCommandLine.TryParse(
            [MigrationVerbs.Validate, "--export", _exportDirectory],
            out _,
            out var error));
        Assert.Contains("--connection", error, StringComparison.Ordinal);
    }

    [Fact]
    public void Usage_tells_the_operator_this_is_not_the_admin_import_screen()
    {
        Assert.Contains("Importación", MigrationCommandLine.Usage, StringComparison.Ordinal);
        Assert.Contains("never over HTTP", MigrationCommandLine.Usage, StringComparison.Ordinal);
    }
}
