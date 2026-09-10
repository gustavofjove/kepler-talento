using KeplerTalento.Domain.Operations;

namespace KeplerTalento.Tools.DataMigration;

/// <summary>
/// Parsed command line for one invocation.
/// </summary>
/// <remarks>
/// Parsing is hand-rolled rather than taken from a package: the surface is three verbs and
/// six options, and principle 2 asks for an explicit reason before a new runtime dependency
/// joins the build. There isn't one here.
/// </remarks>
public sealed record MigrationCommandLine
{
    public required string Verb { get; init; }
    public required string ConnectionString { get; init; }
    public string? ExportDirectory { get; init; }
    public string? MappingFile { get; init; }
    public string? OutputDirectory { get; init; }
    public string? PreMigrationBackup { get; init; }
    public Guid? RunId { get; init; }

    /// <summary>
    /// Overwrites records the application has written since the migration last loaded them.
    /// Off by default: a newer export must not silently undo work done in the app.
    /// </summary>
    public bool OverwriteApplicationEdits { get; init; }

    public const string Usage = """
        ktl-migrate <verb> [options]

        Verbs
          validate   Read the export, resolve references, write a report. Writes no business data.
          load       Validate, then load into PostgreSQL.
          report     Re-emit the report for a recorded run.

        Options
          --connection <string>        Required. Target database, using the MIGRATION role.
          --export <directory>         Export set produced per docs/ktl-7/access-export-procedure.md.
                                       Required for validate and load.
          --mappings <file>            Operator decisions for unresolved catalog values.
          --output <directory>         Where the reconciliation report is written. Default: current directory.
          --pre-migration-backup <label>
                                       Required for load. The backup that undoes this run.
          --overwrite-app-edits        Overwrite records the application changed since they were
                                       loaded. Off by default; those records are skipped and reported.
          --run <guid>                 Required for report. The run to re-emit.

        This tool is not the admin "Importación" screen. It is run by an operator against a
        database, never over HTTP.
        """;

    public static bool TryParse(
        string[] args,
        out MigrationCommandLine commandLine,
        out string error)
    {
        commandLine = null!;
        error = string.Empty;

        if (args.Length == 0)
        {
            error = "A verb is required.";
            return false;
        }

        var verb = args[0];
        if (!MigrationVerbs.IsKnown(verb))
        {
            error = $"Unknown verb '{verb}'. Expected one of: {string.Join(", ", MigrationVerbs.All)}.";
            return false;
        }

        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        var flags = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 1; index < args.Length; index++)
        {
            var argument = args[index];
            if (!argument.StartsWith("--", StringComparison.Ordinal))
            {
                error = $"Unexpected argument '{argument}'.";
                return false;
            }
            if (argument == "--overwrite-app-edits")
            {
                flags.Add(argument);
                continue;
            }
            if (index + 1 >= args.Length)
            {
                error = $"Option '{argument}' needs a value.";
                return false;
            }
            values[argument] = args[++index];
        }

        if (!values.TryGetValue("--connection", out var connection) || string.IsNullOrWhiteSpace(connection))
        {
            error = "Option '--connection' is required.";
            return false;
        }

        Guid? runId = null;
        if (values.TryGetValue("--run", out var runValue))
        {
            if (!Guid.TryParse(runValue, out var parsedRun))
            {
                error = "Option '--run' must be a GUID.";
                return false;
            }
            runId = parsedRun;
        }

        var parsed = new MigrationCommandLine
        {
            Verb = verb,
            ConnectionString = connection,
            ExportDirectory = values.GetValueOrDefault("--export"),
            MappingFile = values.GetValueOrDefault("--mappings"),
            OutputDirectory = values.GetValueOrDefault("--output"),
            PreMigrationBackup = values.GetValueOrDefault("--pre-migration-backup"),
            RunId = runId,
            OverwriteApplicationEdits = flags.Contains("--overwrite-app-edits"),
        };

        if (!parsed.TryValidate(out error))
        {
            return false;
        }

        commandLine = parsed;
        return true;
    }

    private bool TryValidate(out string error)
    {
        error = string.Empty;
        switch (Verb)
        {
            case MigrationVerbs.Validate:
            case MigrationVerbs.Load:
                if (string.IsNullOrWhiteSpace(ExportDirectory))
                {
                    error = $"Option '--export' is required for '{Verb}'.";
                    return false;
                }
                if (!Directory.Exists(ExportDirectory))
                {
                    error = $"Export directory '{ExportDirectory}' does not exist.";
                    return false;
                }
                if (MappingFile is not null && !File.Exists(MappingFile))
                {
                    error = $"Mapping file '{MappingFile}' does not exist.";
                    return false;
                }
                break;
            case MigrationVerbs.Report:
                if (RunId is null)
                {
                    error = "Option '--run' is required for 'report'.";
                    return false;
                }
                break;
            default:
                error = $"Unknown verb '{Verb}'.";
                return false;
        }

        // A load must name the backup that undoes it before it writes anything. This is the
        // one gate the tool refuses to run without.
        if (Verb == MigrationVerbs.Load && string.IsNullOrWhiteSpace(PreMigrationBackup))
        {
            error = "Option '--pre-migration-backup' is required for 'load'. "
                + "Take the backup documented in docs/BACKUP_RESTORE_ROLLBACK_RUNBOOK.md first, "
                + "then pass its label — the report names it as the way to undo this run.";
            return false;
        }

        if (Verb != MigrationVerbs.Load && OverwriteApplicationEdits)
        {
            error = "Option '--overwrite-app-edits' only applies to 'load'.";
            return false;
        }

        return true;
    }

    public string ResolvedOutputDirectory =>
        string.IsNullOrWhiteSpace(OutputDirectory) ? Directory.GetCurrentDirectory() : OutputDirectory;
}
