using KeplerTalento.Tools.TestDataGenerator;

if (!GeneratorOptions.TryParse(args, out var options, out var error))
{
    if (error.Length > 0)
    {
        Console.Error.WriteLine(error);
    }
    Console.Error.WriteLine();
    Console.Error.WriteLine(GeneratorOptions.Usage);
    return error.Length > 0 ? 1 : 0;
}

var exportDirectory = Path.Combine(options.OutputDirectory, "export");
if (Directory.Exists(exportDirectory)
    && Directory.EnumerateFileSystemEntries(exportDirectory).Any()
    && !options.Force)
{
    Console.Error.WriteLine($"'{exportDirectory}' is not empty. Pass --force to overwrite it.");
    return 1;
}
if (options.Force && Directory.Exists(exportDirectory))
{
    Directory.Delete(exportDirectory, recursive: true);
}

var summary = await new ExportSetGenerator(options).WriteAsync(CancellationToken.None);

Console.WriteLine($"Wrote an export set to {Path.GetFullPath(exportDirectory)}.");
Console.WriteLine($"  seed {options.Seed}, as-of {options.AsOf:yyyy-MM-dd} — repeat the run with both to reproduce it.");
Console.WriteLine($"  candidates  {summary.Candidates,7} ({summary.Inactive} logically removed)");
Console.WriteLine($"  languages   {summary.Languages,7}");
Console.WriteLine($"  programs    {summary.Programs,7}");
Console.WriteLine($"  skills      {summary.Skills,7}");
Console.WriteLine($"  education   {summary.Education,7}");
Console.WriteLine($"  experience  {summary.Experience,7}");
Console.WriteLine($"  documents   {summary.Documents,7}");
Console.WriteLine();
Console.WriteLine("This is fabricated personal data. Load it only into a development database.");
Console.WriteLine("Next: ktl-migrate validate --connection <...> --export " + exportDirectory);

return 0;
