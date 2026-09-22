using System.Globalization;
using System.Text;
using KeplerTalento.Application.Abstractions.Correlation;
using KeplerTalento.Application.Abstractions.Identity;
using KeplerTalento.Application.Abstractions.Persistence;
using KeplerTalento.Application.Features.Search;
using KeplerTalento.Domain.Positions;
using KeplerTalento.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;
using Xunit;
using Xunit.Abstractions;

namespace KeplerTalento.Tests.IntegrationTests;

/// <summary>
/// Query-plan evidence for the position list: the default ordering and the normalized
/// title/location "contains" filters stay interactive at scale, and the list projection
/// never reads the description or the requirements.
/// </summary>
/// <remarks>
/// As in <see cref="SearchQueryPlanTests"/>, assertions are about output columns and a cost
/// budget rather than the planner's node choice. <c>EXPLAIN VERBOSE</c> prints every node's
/// output list, which is what proves the bounded projection. The plans are written to
/// <c>docs/ktl-15/query-plans.md</c> so the evidence outlives the run.
/// </remarks>
public sealed class PositionQueryPlanTests(PostgreSqlFixture database, ITestOutputHelper output)
    : IClassFixture<PostgreSqlFixture>
{
    private const int PositionCount = 5_000;
    private const double BudgetMilliseconds = 250;

    [Fact]
    public async Task Representative_position_lists_are_bounded_and_skip_heavy_columns()
    {
        await SeedAsync();
        var report = new StringBuilder();
        report.AppendLine("# KTL-15 position list query plans");
        report.AppendLine();
        report.AppendLine($"Captured by `PositionQueryPlanTests` against {PositionCount:N0} positions, each with a");
        report.AppendLine("representative description and requirements document. Regenerate by running");
        report.AppendLine("`dotnet test backend/Tests/IntegrationTests/IntegrationTests.csproj --filter PositionQueryPlanTests`.");
        report.AppendLine();

        foreach (var (name, options) in Cases())
        {
            var plans = await ExplainAsync(options);
            report.AppendLine($"## {name}");
            report.AppendLine();
            foreach (var (plan, elapsed) in plans)
            {
                var ms = elapsed.ToString("F1", CultureInfo.InvariantCulture);
                output.WriteLine($"=== {name} ({ms} ms) ===");
                output.WriteLine(plan);
                report.AppendLine($"Execution time: {ms} ms");
                report.AppendLine();
                report.AppendLine("```");
                report.AppendLine(plan.TrimEnd());
                report.AppendLine("```");
                report.AppendLine();

                Assert.True(elapsed < BudgetMilliseconds, $"{name} took {ms} ms, beyond the {BudgetMilliseconds} ms budget.\n{plan}");
                foreach (var projected in ProjectedOutputs(plan))
                {
                    Assert.DoesNotContain("\"Description\"", projected, StringComparison.Ordinal);
                    Assert.DoesNotContain("\"Requirements\"", projected, StringComparison.Ordinal);
                }
            }
        }

        var path = Path.Combine(RepositoryRoot(), "docs", "ktl-15", "query-plans.md");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        // LF and a single trailing newline, so the generated file passes the format check.
        var markdown = report.ToString().Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd() + "\n";
        await File.WriteAllTextAsync(path, markdown, new UTF8Encoding(false));
    }

    private static IEnumerable<(string Name, PositionListOptions Options)> Cases()
    {
        yield return ("Default: open, updated descending, first page", new(PositionStatuses.Open, string.Empty, 1, 25, "updatedAt", "desc"));
        yield return ("Title contains, all statuses", new("all", PositionText.Normalize("Desarrollador 12"), 1, 25, "updatedAt", "desc"));
        yield return ("Location contains, closed", new(PositionStatuses.Closed, PositionText.Normalize("Málaga"), 1, 25, "title", "asc"));
        yield return ("Deep page sorted by location", new("all", string.Empty, PositionCount / 100, 100, "location", "asc"));
    }

    /// <summary>
    /// The output lists of every plan node that hands values upward. A scan node reporting
    /// <c>width=0</c> is excluded: that is PostgreSQL's physical-tlist shortcut, which lists the
    /// whole row but passes no column on and never detoasts one, so it reads no description.
    /// </summary>
    private static IEnumerable<string> ProjectedOutputs(string plan)
    {
        var lines = plan.Split('\n');
        for (var index = 1; index < lines.Length; index++)
        {
            if (lines[index].TrimStart().StartsWith("Output:", StringComparison.Ordinal)
                && !lines[index - 1].Contains(" width=0)", StringComparison.Ordinal))
            {
                yield return lines[index];
            }
        }
    }

    /// <summary>Runs the repository's list and explains every statement it sent (count and page).</summary>
    private async Task<List<(string Plan, double Elapsed)>> ExplainAsync(PositionListOptions options)
    {
        var capture = new CommandCapture();
        await using (var dbContext = NewContext(capture))
        {
            await new PositionRepository(dbContext, new NoActor(), new NoCorrelation()).ListAsync(options, CancellationToken.None);
        }

        var results = new List<(string, double)>();
        await using var connection = new NpgsqlConnection(database.ConnectionString);
        await connection.OpenAsync();
        foreach (var (sql, parameters) in capture.Commands)
        {
            await using var command = new NpgsqlCommand($"EXPLAIN (ANALYZE, VERBOSE, BUFFERS) {sql}", connection);
            foreach (var (name, value) in parameters)
            {
                command.Parameters.AddWithValue(name, value ?? DBNull.Value);
            }
            await using var reader = await command.ExecuteReaderAsync();
            var plan = new StringBuilder();
            var elapsed = 0d;
            while (await reader.ReadAsync())
            {
                var line = reader.GetString(0);
                plan.AppendLine(line);
                if (line.StartsWith("Execution Time:", StringComparison.Ordinal))
                {
                    elapsed = double.Parse(line.Split(':')[1].Replace("ms", string.Empty, StringComparison.Ordinal).Trim(), CultureInfo.InvariantCulture);
                }
            }
            results.Add((plan.ToString(), elapsed));
        }
        Assert.Equal(2, results.Count);
        return results;
    }

    private async Task SeedAsync()
    {
        await using var dbContext = NewContext();
        await DatabaseInitializer.MigrateAsync(dbContext, CancellationToken.None);
        if (await dbContext.Positions.CountAsync() >= PositionCount)
        {
            return;
        }
        var cities = new[] { "Madrid", "Málaga", "Bilbao", "Sevilla", "Valencia", "A Coruña", "Remoto" };
        var requirements = SearchFilterDocument.Serialize(SearchFilterNormalization.Normalize(
            new SearchFiltersInput("java", null, [new SearchCriterionInput("Java", "")], "ANY", null, null, null, null, "yes"),
            "Requirements"));
        var description = "<p>" + string.Concat(Enumerable.Repeat("Descripción representativa de la posición. ", 40)) + "</p>";
        var origin = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        for (var index = 0; index < PositionCount; index++)
        {
            var position = new Position(
                Guid.CreateVersion7(), $"Desarrollador {index}", description, cities[index % cities.Length], requirements, 1, origin.AddMinutes(index));
            if (index % 4 == 0)
            {
                position.Update(position.Title, description, position.Location, PositionStatuses.Closed, requirements, 1, origin.AddMinutes(index));
            }
            dbContext.Positions.Add(position);
            if (index % 1000 == 999)
            {
                await dbContext.SaveChangesAsync();
                dbContext.ChangeTracker.Clear();
            }
        }
        await dbContext.SaveChangesAsync();
        await dbContext.Database.ExecuteSqlRawAsync("ANALYZE \"OPS_Positions\"");
    }

    private ApplicationDbContext NewContext() =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(database.ConnectionString).Options);

    private ApplicationDbContext NewContext(CommandCapture capture) => new(
        new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(database.ConnectionString).AddInterceptors(capture).Options);

    private static string RepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !Directory.Exists(Path.Combine(current.FullName, "openspec")))
        {
            current = current.Parent;
        }
        return current?.FullName ?? throw new DirectoryNotFoundException("Could not locate the repository root.");
    }

    /// <summary>Records every command EF sends, so EXPLAIN runs on the exact statements.</summary>
    private sealed class CommandCapture : DbCommandInterceptor
    {
        public List<(string Sql, List<(string Name, object? Value)> Parameters)> Commands { get; } = [];

        public override InterceptionResult<System.Data.Common.DbDataReader> ReaderExecuting(
            System.Data.Common.DbCommand command, CommandEventData eventData, InterceptionResult<System.Data.Common.DbDataReader> result)
        {
            Capture(command);
            return result;
        }

        public override ValueTask<InterceptionResult<System.Data.Common.DbDataReader>> ReaderExecutingAsync(
            System.Data.Common.DbCommand command, CommandEventData eventData, InterceptionResult<System.Data.Common.DbDataReader> result, CancellationToken cancellationToken = default)
        {
            Capture(command);
            return ValueTask.FromResult(result);
        }

        public override InterceptionResult<object> ScalarExecuting(
            System.Data.Common.DbCommand command, CommandEventData eventData, InterceptionResult<object> result)
        {
            Capture(command);
            return result;
        }

        public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
            System.Data.Common.DbCommand command, CommandEventData eventData, InterceptionResult<object> result, CancellationToken cancellationToken = default)
        {
            Capture(command);
            return ValueTask.FromResult(result);
        }

        private void Capture(System.Data.Common.DbCommand command) => Commands.Add((
            command.CommandText,
            [.. command.Parameters.Cast<System.Data.Common.DbParameter>().Select(p => (p.ParameterName, (object?)p.Value))]));
    }

    private sealed class NoActor : ICurrentActor
    {
        public string? ExternalKey => null;
        public Guid? UserId => null;
        public bool IsAuthenticated => false;
        public bool HasPermission(string permission) => false;
    }

    private sealed class NoCorrelation : ICorrelationContext
    {
        public string CorrelationId => string.Empty;
    }
}
