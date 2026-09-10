using KeplerTalento.Domain.Operations;
using KeplerTalento.Infrastructure.Documents;
using KeplerTalento.Infrastructure.Persistence;
using KeplerTalento.Tools.DataMigration;
using Microsoft.EntityFrameworkCore;

if (args.Length == 1 && (args[0] == "--help" || args[0] == "-h"))
{
    Console.WriteLine(MigrationCommandLine.Usage);
    return ExitCodes.Success;
}

if (!MigrationCommandLine.TryParse(args, out var commandLine, out var parseError))
{
    Console.Error.WriteLine(parseError);
    Console.Error.WriteLine();
    Console.Error.WriteLine(MigrationCommandLine.Usage);
    return ExitCodes.UsageError;
}

var options = new DbContextOptionsBuilder<ApplicationDbContext>()
    .UseNpgsql(commandLine.ConnectionString)
    .Options;

var run = new MigrationRun(
    Guid.CreateVersion7(),
    commandLine.Verb,
    commandLine.PreMigrationBackup,
    DateTimeOffset.UtcNow);

// Every invocation is recorded before it does anything, so a run that dies partway still
// leaves evidence that it happened.
await using (var dbContext = new ApplicationDbContext(options))
{
    dbContext.MigrationRuns.Add(run);
    await dbContext.SaveChangesAsync();
}

Console.WriteLine($"Run {run.Id}: {run.Verb}");
if (run.BackupLabel is not null)
{
    Console.WriteLine($"Pre-migration backup: {run.BackupLabel}");
}
if (commandLine.OverwriteApplicationEdits)
{
    Console.WriteLine(
        "Records changed in the application since they were loaded WILL be overwritten.");
}

var storageOptions = new DocumentStorageOptions
{
    Root = Environment.GetEnvironmentVariable("DocumentStorage__Root") ?? "document-storage",
};

try
{
    FileSystemDocumentStorage.ValidateAndPrepare(storageOptions);
    var storage = new FileSystemDocumentStorage(storageOptions);
    var clamAvOptions = new ClamAvOptions();
    var runner = new MigrationRunner(
        commandLine,
        storage,
        new DocumentContentInspector(),
        new ClamAvScanner(clamAvOptions),
        storageOptions.MaximumBytes,
        Console.Out,
        Console.Error);
    return await runner.RunAsync(run, CancellationToken.None);
}
catch (Exception exception)
{
    // The type is written, never the message: an exception raised while reading a row can
    // quote that row, and rows here are candidate personal data.
    Console.Error.WriteLine($"Run {run.Id} failed: {exception.GetType().Name}.");
    await using var dbContext = new ApplicationDbContext(options);
    var tracked = await dbContext.MigrationRuns.SingleAsync(value => value.Id == run.Id);
    tracked.Finish(MigrationOutcomes.Failed, default, reportJson: null, DateTimeOffset.UtcNow);
    await dbContext.SaveChangesAsync();
    return ExitCodes.Failed;
}

internal static class ExitCodes
{
    public const int Success = 0;
    public const int UsageError = 2;
    public const int Failed = 3;
    public const int NotReconciled = 4;
}
