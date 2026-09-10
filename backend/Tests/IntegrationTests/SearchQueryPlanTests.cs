using System.Text;
using KeplerTalento.Application.Features.Search;
using KeplerTalento.Domain.Candidates;
using KeplerTalento.Domain.Catalogs;
using KeplerTalento.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;
using Xunit;
using Xunit.Abstractions;

namespace KeplerTalento.Tests.IntegrationTests;

/// <summary>
/// Query-plan evidence for the design's index decision.
/// </summary>
/// <remarks>
/// The dataset is generated at the scale KTL-7 reconciled — a few thousand candidates with
/// their relations and documents — because a plan against nine fixture rows says nothing: at
/// that size PostgreSQL picks a sequential scan for everything and would "prove" any index
/// decision equally well.
///
/// The assertions are deliberately about shape and cost bounds rather than about which node
/// the planner chose. A test that pins the plan node fails the first time PostgreSQL's cost
/// model improves, which teaches everyone to ignore it. What must hold is that no search
/// degenerates into scanning the relation tables per candidate, and that the whole thing
/// stays interactive.
///
/// The captured plans are written to <c>docs/ktl-10/query-plans.md</c> so the evidence
/// outlives the test run, as the design requires.
/// </remarks>
public sealed class SearchQueryPlanTests(PostgreSqlFixture database, ITestOutputHelper output)
    : IClassFixture<PostgreSqlFixture>
{
    private const int CandidateCount = 3_000;

    /// <summary>The interactive budget one search must stay inside at this scale.</summary>
    private const double BudgetMilliseconds = 500;

    [Fact]
    public async Task Representative_searches_stay_within_the_interactive_budget()
    {
        var catalog = await SeedScaleDatasetAsync();
        var report = new StringBuilder();
        report.AppendLine("# KTL-10 query plans");
        report.AppendLine();
        report.AppendLine(
            $"Captured by `SearchQueryPlanTests` against {CandidateCount:N0} candidates with their");
        report.AppendLine(
            "relations and primary documents — the scale KTL-7 reconciled. Regenerate by running");
        report.AppendLine("`dotnet test backend/Tests/IntegrationTests/IntegrationTests.csproj`.");
        report.AppendLine();

        foreach (var (name, filters) in Cases(catalog))
        {
            var (plan, elapsed) = await ExplainAsync(filters);
            output.WriteLine($"=== {name} ({elapsed.ToString("F1", System.Globalization.CultureInfo.InvariantCulture)} ms) ===");
            output.WriteLine(plan);
            report.AppendLine($"## {name}");
            report.AppendLine();
            report.AppendLine($"Execution time: {elapsed.ToString("F1", System.Globalization.CultureInfo.InvariantCulture)} ms");
            report.AppendLine();
            report.AppendLine("```");
            report.AppendLine(plan.TrimEnd());
            report.AppendLine("```");
            report.AppendLine();

            Assert.True(
                elapsed < BudgetMilliseconds,
                $"{name} took {elapsed.ToString("F1", System.Globalization.CultureInfo.InvariantCulture)} ms, beyond the {BudgetMilliseconds} ms budget. "
                + "The design must be amended before an index is added in response.\n" + plan);
            // A nested loop that re-scans a relation table for every candidate is the failure
            // mode correlated EXISTS predicates are supposed to avoid.
            Assert.DoesNotContain("Seq Scan on \"CND_CandidateSkills\"", plan, StringComparison.Ordinal);
            Assert.DoesNotContain("Seq Scan on \"CND_CandidateLanguages\"", plan, StringComparison.Ordinal);
            Assert.DoesNotContain("Seq Scan on \"CND_CandidatePrograms\"", plan, StringComparison.Ordinal);
        }

        var path = Path.Combine(RepositoryRoot(), "docs", "ktl-10", "query-plans.md");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, report.ToString(), new UTF8Encoding(false));
    }

    private static IEnumerable<(string Name, SearchFiltersValue Filters)> Cases(ScaleCatalog catalog)
    {
        SearchFiltersValue Build(SearchFiltersInput input) =>
            SearchFilterNormalization.Normalize(input, "Filters");

        yield return (
            "Unfiltered first page",
            Build(new SearchFiltersInput(null, null, null, null, null, null, null, null, null)));
        yield return (
            "Free text (leading wildcard)",
            Build(new SearchFiltersInput("ez 1234", null, null, null, null, null, null, null, null)));
        yield return (
            "Status subset and primary CV",
            Build(new SearchFiltersInput(
                null,
                [CandidateStatuses.Available, CandidateStatuses.InProcess],
                null, null, null, null, null, null,
                "yes")));
        yield return (
            "Skill ANY across three values",
            Build(new SearchFiltersInput(
                null,
                null,
                [
                    new SearchCriterionInput(catalog.Skills[0], ""),
                    new SearchCriterionInput(catalog.Skills[1], ""),
                    new SearchCriterionInput(catalog.Skills[2], ""),
                ],
                "ANY",
                null, null, null, null, null)));
        yield return (
            "Skill ALL across three values",
            Build(new SearchFiltersInput(
                null,
                null,
                [
                    new SearchCriterionInput(catalog.Skills[0], catalog.SkillLevels[0]),
                    new SearchCriterionInput(catalog.Skills[1], ""),
                    new SearchCriterionInput(catalog.Skills[2], ""),
                ],
                "ALL",
                null, null, null, null, null)));
        yield return (
            "Every family combined",
            Build(new SearchFiltersInput(
                "ez",
                [CandidateStatuses.Available],
                [new SearchCriterionInput(catalog.Skills[0], "")],
                "ANY",
                [new SearchCriterionInput(catalog.Languages[0], catalog.LanguageLevels[0])],
                "ALL",
                [new SearchCriterionInput(catalog.Programs[0], "")],
                "ANY",
                "yes")));
    }

    private async Task<(string Plan, double ElapsedMilliseconds)> ExplainAsync(SearchFiltersValue filters)
    {
        // The command is captured as EF sends it, parameters and all, rather than
        // reconstructed: a plan for a reconstruction of the query would prove nothing about
        // the query. ToQueryString is not usable here — it renders parameter values as
        // comments, which PostgreSQL cannot execute.
        var capture = new CommandCapture();
        await using (var dbContext = NewContext(capture))
        {
            var search = new CandidateSearchQuery(dbContext);
            var matching = await search.MatchingAsync(filters, CancellationToken.None);
            await search.Page(matching, 1, SearchPaging.DefaultPageSize).ToListAsync();
        }

        await using var connection = new NpgsqlConnection(database.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand($"EXPLAIN (ANALYZE, BUFFERS) {capture.Sql}", connection);
        foreach (var (name, value) in capture.Parameters)
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
                elapsed = double.Parse(
                    line.Split(':')[1].Replace("ms", string.Empty, StringComparison.Ordinal).Trim(),
                    System.Globalization.CultureInfo.InvariantCulture);
            }
        }
        return (plan.ToString(), elapsed);
    }

    /// <summary>Records the last command EF sent, so EXPLAIN can be run on that exact statement.</summary>
    private sealed class CommandCapture : DbCommandInterceptor
    {
        public string Sql { get; private set; } = string.Empty;
        public List<(string Name, object? Value)> Parameters { get; } = [];

        public override InterceptionResult<System.Data.Common.DbDataReader> ReaderExecuting(
            System.Data.Common.DbCommand command,
            CommandEventData eventData,
            InterceptionResult<System.Data.Common.DbDataReader> result)
        {
            Capture(command);
            return result;
        }

        public override ValueTask<InterceptionResult<System.Data.Common.DbDataReader>> ReaderExecutingAsync(
            System.Data.Common.DbCommand command,
            CommandEventData eventData,
            InterceptionResult<System.Data.Common.DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            Capture(command);
            return ValueTask.FromResult(result);
        }

        private void Capture(System.Data.Common.DbCommand command)
        {
            Sql = command.CommandText;
            Parameters.Clear();
            foreach (System.Data.Common.DbParameter parameter in command.Parameters)
            {
                Parameters.Add((parameter.ParameterName, parameter.Value));
            }
        }
    }

    private sealed record ScaleCatalog(
        IReadOnlyList<string> Skills,
        IReadOnlyList<string> SkillLevels,
        IReadOnlyList<string> Languages,
        IReadOnlyList<string> LanguageLevels,
        IReadOnlyList<string> Programs,
        IReadOnlyList<string> ProgramLevels);

    private async Task<ScaleCatalog> SeedScaleDatasetAsync()
    {
        await using var dbContext = NewContext();
        await DatabaseInitializer.MigrateAsync(dbContext, CancellationToken.None);
        if (await dbContext.Candidates.CountAsync() >= CandidateCount)
        {
            return await ReadCatalogAsync(dbContext);
        }
        await DatabaseInitializer.SeedCatalogsAsync(dbContext, CancellationToken.None);
        var catalog = await ReadCatalogAsync(dbContext);
        var items = await dbContext.CatalogItems.AsNoTracking().ToListAsync();
        Guid Id(string family, string name) => items
            .First(item => item.Family == family && item.NameNormalized == CatalogName.Normalize(name))
            .Id;

        var origin = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var random = new Random(20261010);
        for (var index = 0; index < CandidateCount; index++)
        {
            var id = Guid.CreateVersion7();
            var candidate = new Candidate(id, $"Nombre{index}", $"Apellido{index}", origin);
            candidate.SetDetails(
                phone: $"+34 6{index:0000000}",
                email: $"persona{index}@ejemplo.test",
                location: "Madrid",
                province: "Madrid",
                country: "España",
                availability: "Inmediata",
                status: CandidateStatuses.All[index % CandidateStatuses.All.Count],
                source: "escala",
                notes: $"Perfil sintético número {index} con notas de longitud representativa.",
                updatedAtUtc: origin.AddMinutes(index));
            dbContext.Candidates.Add(candidate);

            foreach (var skillName in catalog.Skills.OrderBy(_ => random.Next()).Take(3))
            {
                dbContext.CandidateSkills.Add(new CandidateSkill(
                    Guid.CreateVersion7(),
                    id,
                    Id(CatalogFamilies.Skill, skillName),
                    Id(CatalogFamilies.SkillLevel, catalog.SkillLevels[random.Next(catalog.SkillLevels.Count)])));
            }
            dbContext.CandidateLanguages.Add(new CandidateLanguage(
                Guid.CreateVersion7(),
                id,
                Id(CatalogFamilies.Language, catalog.Languages[random.Next(catalog.Languages.Count)]),
                Id(CatalogFamilies.LanguageLevel, catalog.LanguageLevels[random.Next(catalog.LanguageLevels.Count)])));
            dbContext.CandidatePrograms.Add(new CandidateProgram(
                Guid.CreateVersion7(),
                id,
                Id(CatalogFamilies.Program, catalog.Programs[random.Next(catalog.Programs.Count)]),
                Id(CatalogFamilies.ProgramLevel, catalog.ProgramLevels[random.Next(catalog.ProgramLevels.Count)])));

            // Two candidates in three have a primary CV, so the CV predicate is selective
            // rather than trivially true or trivially false.
            if (index % 3 != 0)
            {
                var documentId = Guid.CreateVersion7();
                var document = new Domain.Documents.CandidateDocument(
                    documentId,
                    id,
                    $"{id:N}/{documentId:N}.bin",
                    "cv.pdf",
                    "application/pdf",
                    1024,
                    new string('a', 64),
                    origin);
                document.SetDocumentType("cv");
                document.SetPrimary(true, origin);
                dbContext.Documents.Add(document);
            }

            if (index % 500 == 499)
            {
                await dbContext.SaveChangesAsync();
                dbContext.ChangeTracker.Clear();
            }
        }
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();
        // Without fresh statistics the planner works from defaults, and the captured plan
        // describes a database nobody runs.
        await dbContext.Database.ExecuteSqlRawAsync("ANALYZE");
        return catalog;
    }

    private static async Task<ScaleCatalog> ReadCatalogAsync(ApplicationDbContext dbContext)
    {
        async Task<IReadOnlyList<string>> Family(string family) => await dbContext.CatalogItems
            .AsNoTracking()
            .Where(item => item.Family == family)
            .OrderBy(item => item.SortOrder)
            .Select(item => item.NameEs)
            .ToListAsync();

        return new ScaleCatalog(
            await Family(CatalogFamilies.Skill),
            await Family(CatalogFamilies.SkillLevel),
            await Family(CatalogFamilies.Language),
            await Family(CatalogFamilies.LanguageLevel),
            await Family(CatalogFamilies.Program),
            await Family(CatalogFamilies.ProgramLevel));
    }

    private DbContextOptions<ApplicationDbContext> Options =>
        new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(database.ConnectionString).Options;

    private ApplicationDbContext NewContext() => new(Options);

    private ApplicationDbContext NewContext(CommandCapture capture) => new(
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(database.ConnectionString)
            .AddInterceptors(capture)
            .Options);

    private static string RepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !Directory.Exists(Path.Combine(current.FullName, "openspec")))
        {
            current = current.Parent;
        }
        return current?.FullName ?? throw new DirectoryNotFoundException("Could not locate the repository root.");
    }
}
