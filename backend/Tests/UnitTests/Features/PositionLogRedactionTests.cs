using System.Collections.Concurrent;
using KeplerTalento.Web.Observability;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Xunit;

namespace KeplerTalento.Tests.UnitTests.Features;

/// <summary>
/// A position's title, location, description and requirements must not reach application
/// logs, whether logged directly, destructured from a request or carried by a failure.
/// </summary>
/// <remarks>
/// Checked by sentinel value, like the candidate and search redaction tests, so it stays
/// true as the enricher's rule table grows.
/// </remarks>
public sealed class PositionLogRedactionTests
{
    private const string TitleSentinel = "Sustituta de Marta Ruiz Etxeberria";
    private const string LocationSentinel = "Villaposición del Centinela";
    private const string DescriptionSentinel = "<p onclick='centinela()'>Baja de Marta</p>";
    private const string CriterionSentinel = "Cobol-Centinela-Posición";

    [Fact]
    public void A_position_write_logs_none_of_its_values()
    {
        using var sink = new InMemorySink();
        using var logger = Build(sink);

        logger.Information("Position stored {@Position}", PositionPayload());
        logger.ForContext("title", TitleSentinel)
            .ForContext("normalizedTitle", TitleSentinel.ToLowerInvariant())
            .ForContext("location", LocationSentinel)
            .ForContext("normalizedLocation", LocationSentinel.ToLowerInvariant())
            .ForContext("description", DescriptionSentinel)
            .Information("Position stored");

        AssertNoSentinels(sink);
    }

    [Fact]
    public void A_failing_position_write_logs_none_of_its_values()
    {
        using var sink = new InMemorySink();
        using var logger = Build(sink);

        logger.Error(
            new InvalidOperationException("fallo sintético"),
            "Position write failed {@Request}",
            new Dictionary<string, object>
            {
                ["operation"] = "positions.update",
                ["position"] = PositionPayload(),
            });

        AssertNoSentinels(sink);
    }

    [Fact]
    public void Position_identifiers_and_outcomes_still_reach_the_sink()
    {
        using var sink = new InMemorySink();
        using var logger = Build(sink);

        logger.Information(
            "Position stored {PositionId} {Status} {Outcome}",
            "01a08b17-58a0-777e-8e11-000000000000",
            "closed",
            "ok");

        var rendered = Rendered(sink);
        Assert.Contains("01a08b17-58a0-777e-8e11-000000000000", rendered, StringComparison.Ordinal);
        Assert.Contains("closed", rendered, StringComparison.Ordinal);
    }

    private static Dictionary<string, object> PositionPayload() => new()
    {
        ["id"] = "01a08b17-58a0-777e-8e11-000000000000",
        ["title"] = TitleSentinel,
        ["location"] = LocationSentinel,
        ["description"] = DescriptionSentinel,
        ["requirements"] = new Dictionary<string, object>
        {
            ["text"] = CriterionSentinel,
            ["skillCriteria"] = new[] { new Dictionary<string, object> { ["value"] = CriterionSentinel, ["level"] = "" } },
        },
    };

    private static void AssertNoSentinels(InMemorySink sink)
    {
        var rendered = Rendered(sink);
        foreach (var sentinel in new[] { TitleSentinel, LocationSentinel, DescriptionSentinel, CriterionSentinel, "centinela()" })
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
