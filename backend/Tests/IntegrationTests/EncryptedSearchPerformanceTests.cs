using System.Diagnostics;
using System.Globalization;
using System.Text;
using KeplerTalento.Application.Features.Search;
using KeplerTalento.Domain.Candidates;
using KeplerTalento.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;
using Xunit.Abstractions;

namespace KeplerTalento.Tests.IntegrationTests;

/// <summary>
/// KTL-33 performance evidence: free text and the last-name order run in the API over decrypted
/// values, so their cost grows with the rows the other filters leave. This measures them at the
/// documented ceiling against the documented budget.
/// </summary>
/// <remarks>
/// Measured through <see cref="CandidateSearchQuery.SearchAsync"/>, the statement sequence the API
/// issues, not a reconstruction. HTTP and JSON are excluded: they cost the same as before KTL-33.
/// The measured values are written to <c>docs/ktl-33/performance.md</c>.
/// </remarks>
[Collection(TimingCollection.Name)]
public sealed class EncryptedSearchPerformanceTests(PostgreSqlFixture database, ITestOutputHelper output)
    : IClassFixture<PostgreSqlFixture>
{
    /// <summary>The documented ceiling of the in-API approach (candidate-search spec).</summary>
    private const int CandidateCount = 12_000;

    /// <summary>The documented p95 budget at the ceiling.</summary>
    private const double BudgetMilliseconds = 300;

    private const int Warmups = 3;
    private const int Samples = 20;

    [Fact]
    public async Task Text_search_and_last_name_order_meet_the_budget_at_the_ceiling()
    {
        await SeedAsync();
        var report = new StringBuilder();
        report.AppendLine("# KTL-33 search performance over encrypted fields");
        report.AppendLine();
        report.AppendLine(
            $"Captured by `EncryptedSearchPerformanceTests` against {CandidateCount.ToString("N0", CultureInfo.InvariantCulture)} active candidates");
        report.AppendLine(
            "with notes of 200–600 characters, in a Testcontainers `postgres:17.6-alpine` on the developer machine.");
        report.AppendLine(
            $"Each case: {Warmups} warm-up runs, then {Samples} timed runs of `CandidateSearchQuery.SearchAsync` (every");
        report.AppendLine(
            "statement plus decryption, matching, sorting and paging; HTTP and JSON excluded).");
        report.AppendLine($"Budget: p95 ≤ {BudgetMilliseconds.ToString(CultureInfo.InvariantCulture)} ms at the ceiling.");
        report.AppendLine();
        report.AppendLine("| Case | Median (ms) | p95 (ms) | Max (ms) |");
        report.AppendLine("| ---- | ----------: | -------: | -------: |");

        var failures = new List<string>();
        foreach (var (name, filters, options) in Cases())
        {
            var timings = new List<double>();
            for (var run = 0; run < Warmups + Samples; run++)
            {
                await using var dbContext = NewContext();
                var watch = Stopwatch.StartNew();
                var page = await new CandidateSearchQuery(dbContext).SearchAsync(filters, options, CancellationToken.None);
                watch.Stop();
                Assert.True(page.TotalCount >= 0);
                if (run >= Warmups)
                {
                    timings.Add(watch.Elapsed.TotalMilliseconds);
                }
            }
            timings.Sort();
            var median = timings[timings.Count / 2];
            var p95 = timings[(int)Math.Ceiling(timings.Count * 0.95) - 1];
            var max = timings[^1];
            output.WriteLine($"{name}: median {median:F1} ms, p95 {p95:F1} ms, max {max:F1} ms");
            report.AppendLine(string.Create(
                CultureInfo.InvariantCulture,
                $"| {name} | {median:F1} | {p95:F1} | {max:F1} |"));
            if (p95 > BudgetMilliseconds)
            {
                failures.Add($"{name}: p95 {p95:F1} ms");
            }
        }

        report.AppendLine();
        report.AppendLine("Searches with neither free text nor the last-name order are unchanged by KTL-33 and");
        report.AppendLine("keep their SQL plan evidence in [`docs/ktl-10/query-plans.md`](../ktl-10/query-plans.md).");
        var path = Path.Combine(RepositoryRoot(), "docs", "ktl-33", "performance.md");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, report.ToString(), new UTF8Encoding(false));

        Assert.True(failures.Count == 0, "Beyond the budget: " + string.Join("; ", failures));
    }

    private static IEnumerable<(string Name, SearchFiltersValue Filters, SearchOptions Options)> Cases()
    {
        SearchFiltersValue Text(string? text, string[]? statuses = null) => SearchFilterNormalization.Normalize(
            new SearchFiltersInput(text, statuses, null, null, null, null, null, null, null),
            "Filters");
        var byDate = SearchSort.Default;
        var byName = new SearchSort(SearchSortField.LastName, SearchSortDirection.Ascending);
        var deepPage = CandidateCount / SearchPaging.MaximumPageSize;

        yield return ("Text matching one candidate", Text("persona1234@"), new SearchOptions(1, SearchPaging.DefaultPageSize, byDate, false));
        yield return ("Text matching most candidates", Text("ejemplo"), new SearchOptions(1, SearchPaging.DefaultPageSize, byDate, false));
        yield return ("Text found only in notes", Text("referencia 777"), new SearchOptions(1, SearchPaging.DefaultPageSize, byDate, false));
        yield return ("Text matching nothing", Text("no-existe-nadie"), new SearchOptions(1, SearchPaging.DefaultPageSize, byDate, false));
        yield return ("Last-name order, first page", Text(null), new SearchOptions(1, SearchPaging.DefaultPageSize, byName, false));
        yield return ($"Last-name order, deep page {deepPage}", Text(null), new SearchOptions(deepPage, SearchPaging.MaximumPageSize, byName, true));
        yield return ("Text and status, last-name order", Text("ejemplo", [CandidateStatuses.Available]), new SearchOptions(1, SearchPaging.DefaultPageSize, byName, false));
    }

    private async Task SeedAsync()
    {
        await using var dbContext = NewContext();
        await DatabaseInitializer.MigrateAsync(dbContext, CancellationToken.None);
        if (await dbContext.Candidates.CountAsync() >= CandidateCount)
        {
            return;
        }
        await DatabaseInitializer.SeedCatalogsAsync(dbContext, CancellationToken.None);

        var origin = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var random = new Random(20260929);
        const string filler = "Experiencia en atención al cliente, gestión de equipos y coordinación de proyectos. ";
        for (var index = 0; index < CandidateCount; index++)
        {
            var candidate = new Candidate(Guid.CreateVersion7(), $"Nombre{index}", $"Apellido{random.Next(100_000):00000}", origin);
            var notes = new StringBuilder($"Referencia {index}. ");
            var length = random.Next(200, 601);
            while (notes.Length < length)
            {
                notes.Append(filler);
            }
            candidate.SetDetails(
                phone: $"+34 6{index:0000000}",
                email: $"persona{index}@ejemplo.test",
                location: "Madrid",
                province: "Madrid",
                country: "España",
                availability: "Inmediata",
                status: CandidateStatuses.All[index % CandidateStatuses.All.Count],
                source: "escala",
                notes: notes.ToString(0, length),
                updatedAtUtc: origin.AddMinutes(index));
            dbContext.Candidates.Add(candidate);
            if (index % 1000 == 999)
            {
                await dbContext.SaveChangesAsync();
                dbContext.ChangeTracker.Clear();
            }
        }
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();
        await dbContext.Database.ExecuteSqlRawAsync("ANALYZE");
    }

    private ApplicationDbContext NewContext() => new(
        new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(database.ConnectionString).UseTestFieldEncryption().Options);

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
