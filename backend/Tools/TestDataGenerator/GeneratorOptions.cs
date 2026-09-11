using System.Globalization;

namespace KeplerTalento.Tools.TestDataGenerator;

/// <summary>
/// Parsed command line for one generator run. Hand-rolled for the same reason
/// <c>MigrationCommandLine</c> is: five options do not justify a dependency.
/// </summary>
public sealed record GeneratorOptions
{
    public required string OutputDirectory { get; init; }
    public int Candidates { get; init; } = 500;
    public int Seed { get; init; } = 20260911;
    public bool Force { get; init; }

    /// <summary>
    /// The date every generated date is measured back from — an application arrives so many
    /// days before it, a review falls due two years after consent, an open period runs up to
    /// it. Defaults to today so a fresh run produces a recent-looking dataset.
    /// </summary>
    /// <remarks>
    /// It is an option rather than <c>DateTime.UtcNow</c> read inside the generator because
    /// the wall clock would otherwise silently enter the output: the same seed run on two
    /// different days would produce different dates, and a dataset that exposed a defect
    /// could not be reproduced afterwards. Seed and anchor together identify a set; the
    /// summary prints both so a run can be repeated exactly.
    /// </remarks>
    public DateOnly AsOf { get; init; } = DateOnly.FromDateTime(DateTime.UtcNow);

    public const string Usage = """
        ktl-testdata --output <directory> [options]

        Writes a synthetic export set in the shape ktl-migrate consumes: the seven CSV files
        of docs/ktl-7/access-export-procedure.md plus a files/ directory of CV stand-ins.

        Options
          --output <directory>   Required. Where the export set is written.
          --candidates <n>       How many candidates to generate. Default 500.
          --seed <n>             Random seed. Default 20260911.
          --as-of <yyyy-MM-dd>   Date the generated dates are measured back from.
                                 Defaults to today, so a fresh run looks recent.
          --force                Overwrite a non-empty output directory.

        The same --seed and --as-of reproduce the same set exactly, byte for byte. Both are
        printed on completion, so a set that exposes a defect can be regenerated later.

        Every catalog reference is drawn from CatalogSeedData, so a run over the result
        reports no unresolved values and no rejected rows.

        This writes fabricated personal data. It is for development and testing only, and it
        must never be loaded into a database that also holds real candidates.
        """;

    public static bool TryParse(string[] args, out GeneratorOptions options, out string error)
    {
        options = null!;
        error = string.Empty;

        string? output = null;
        var candidates = 500;
        var seed = 20260911;
        var force = false;
        var asOf = DateOnly.FromDateTime(DateTime.UtcNow);

        for (var index = 0; index < args.Length; index++)
        {
            var argument = args[index];
            switch (argument)
            {
                case "--force":
                    force = true;
                    continue;
                case "--help" or "-h":
                    error = string.Empty;
                    return false;
            }

            if (!argument.StartsWith("--", StringComparison.Ordinal))
            {
                error = $"Unexpected argument '{argument}'.";
                return false;
            }
            if (index + 1 >= args.Length)
            {
                error = $"Option '{argument}' requires a value.";
                return false;
            }

            var value = args[++index];
            switch (argument)
            {
                case "--output":
                    output = value;
                    break;
                case "--candidates":
                    if (!int.TryParse(value, out candidates) || candidates is < 1 or > 100_000)
                    {
                        error = "--candidates must be between 1 and 100000.";
                        return false;
                    }
                    break;
                case "--seed":
                    if (!int.TryParse(value, out seed))
                    {
                        error = "--seed must be an integer.";
                        return false;
                    }
                    break;
                case "--as-of":
                    // Exact format only, matching the export contract's own date encoding:
                    // a culture-dependent parse would make the same command line mean
                    // different things on different machines.
                    if (!DateOnly.TryParseExact(
                            value,
                            "yyyy-MM-dd",
                            CultureInfo.InvariantCulture,
                            DateTimeStyles.None,
                            out asOf))
                    {
                        error = "--as-of must be a date in the form yyyy-MM-dd.";
                        return false;
                    }
                    break;
                default:
                    error = $"Unknown option '{argument}'.";
                    return false;
            }
        }

        if (string.IsNullOrWhiteSpace(output))
        {
            error = "--output is required.";
            return false;
        }

        options = new GeneratorOptions
        {
            OutputDirectory = output,
            Candidates = candidates,
            Seed = seed,
            Force = force,
            AsOf = asOf,
        };
        return true;
    }
}
