using System.Collections.Concurrent;
using KeplerTalento.Web.Observability;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Xunit;

namespace KeplerTalento.Tests.UnitTests.Features;

/// <summary>
/// Search terms, filter payloads and saved-search names must not reach application logs,
/// on the successful path or the failing one.
/// </summary>
/// <remarks>
/// A search term is at least as personal as the field it matched: "Marta Ruiz" in a log is
/// the same disclosure whether it arrived as a candidate's name or as the query for it. And
/// an employee's saved search is routinely named after a person.
///
/// Like the candidate redaction tests, this checks by sentinel value rather than by
/// restating the enricher's rule table, so it keeps being true as slices are added.
/// </remarks>
public sealed class SearchLogRedactionTests
{
    private const string TermSentinel = "Marta Ruiz Etxeberria";
    private const string PresetSentinel = "Candidatos de Marta para Bilbao";
    private const string CriterionSentinel = "Cobol-Centinela";

    [Fact]
    public void A_successful_search_logs_no_term_or_filter_payload()
    {
        using var sink = new InMemorySink();
        using var logger = Build(sink);

        logger.Information("Search completed {@Filters} {Outcome}", SearchPayload(), "ok");
        logger.ForContext("text", TermSentinel).Information("Search completed");

        AssertNoSentinels(sink);
    }

    [Fact]
    public void A_failing_search_logs_no_term_or_filter_payload()
    {
        using var sink = new InMemorySink();
        using var logger = Build(sink);

        logger.Error(
            new InvalidOperationException("fallo sintético"),
            "Search failed {@Request}",
            new Dictionary<string, object>
            {
                ["operation"] = "candidates.search",
                ["filters"] = SearchPayload(),
            });

        AssertNoSentinels(sink);
    }

    [Fact]
    public void A_preset_operation_logs_neither_its_name_nor_its_filters()
    {
        using var sink = new InMemorySink();
        using var logger = Build(sink);

        logger.Information(
            "Preset stored {@Preset}",
            new Dictionary<string, object>
            {
                ["presetId"] = "01a08b17-58a0-777e-8e11-000000000000",
                ["name"] = PresetSentinel,
                ["filters"] = SearchPayload(),
            });
        logger.ForContext("presetName", PresetSentinel).Error("Preset write failed");
        logger.ForContext("normalizedName", PresetSentinel.ToLowerInvariant()).Error("Preset conflict");

        AssertNoSentinels(sink);
    }

    [Fact]
    public void Safe_operational_metadata_still_reaches_the_sink()
    {
        using var sink = new InMemorySink();
        using var logger = Build(sink);

        // Redaction that swallowed these would make the logs useless for the thing they are
        // for: relating an outcome to a request and an actor.
        logger.Information(
            "Search completed {CorrelationId} {PresetId} {Outcome} {TotalCount}",
            "8ce2bbb29a684adbb9877e7ccb3e5682",
            "01a08b17-58a0-777e-8e11-000000000000",
            "ok",
            42);

        var rendered = Rendered(sink);
        Assert.Contains("8ce2bbb29a684adbb9877e7ccb3e5682", rendered, StringComparison.Ordinal);
        Assert.Contains("01a08b17-58a0-777e-8e11-000000000000", rendered, StringComparison.Ordinal);
        Assert.Contains("42", rendered, StringComparison.Ordinal);
    }

    private static Dictionary<string, object> SearchPayload() => new()
    {
        ["text"] = TermSentinel,
        ["skillCriteria"] = new[] { new Dictionary<string, object> { ["value"] = CriterionSentinel, ["level"] = "Alto" } },
        ["languageCriteria"] = new[] { new Dictionary<string, object> { ["value"] = CriterionSentinel, ["level"] = "" } },
        ["programCriteria"] = new[] { new Dictionary<string, object> { ["value"] = CriterionSentinel, ["level"] = "" } },
    };

    private static void AssertNoSentinels(InMemorySink sink)
    {
        var rendered = Rendered(sink);
        foreach (var sentinel in new[] { TermSentinel, PresetSentinel, CriterionSentinel })
        {
            Assert.DoesNotContain(sentinel, rendered, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static string Rendered(InMemorySink sink) =>
        string.Join(
            '\n',
            sink.LogEvents.Select(logEvent =>
                logEvent.RenderMessage() + ' ' + string.Join(
                    ' ',
                    logEvent.Properties.Select(property => $"{property.Key}={property.Value}"))));

    private static Logger Build(InMemorySink sink) => new LoggerConfiguration()
        .MinimumLevel.Is(LogEventLevel.Verbose)
        .Enrich.With<PersonalDataRedactionEnricher>()
        .WriteTo.Sink(sink)
        .CreateLogger();

    private sealed class InMemorySink : ILogEventSink, IDisposable
    {
        private readonly ConcurrentQueue<LogEvent> _events = new();

        public IEnumerable<LogEvent> LogEvents => _events;

        public void Emit(LogEvent logEvent) => _events.Enqueue(logEvent);

        public void Dispose() => _events.Clear();
    }
}
