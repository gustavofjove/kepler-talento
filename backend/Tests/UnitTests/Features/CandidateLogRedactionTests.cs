using System.Collections.Concurrent;
using KeplerTalento.Web.Observability;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Xunit;

namespace KeplerTalento.Tests.UnitTests.Features;

/// <summary>
/// Candidate personal data must not reach application logs, including when a request
/// fails.
/// </summary>
/// <remarks>
/// The check is by sentinel value rather than by inspecting the enricher's rules: a test
/// that asserts "these distinctive strings appear nowhere in the output" keeps being true
/// as slices are added, whereas one that asserts the rule table is a restatement of the
/// implementation.
///
/// The redaction is by property name, and the limit of that is worth stating: a candidate
/// value logged under an unrelated property name, or pre-formatted into a message string,
/// is not something this can catch. What it does guarantee is that the candidate fields,
/// logged as themselves — directly or nested inside a destructured object — never reach a
/// sink.
/// </remarks>
public sealed class CandidateLogRedactionTests
{
    /// <summary>Values distinctive enough that finding one in the output is unambiguous.</summary>
    private static readonly (string Property, string Value)[] Sentinels =
    [
        ("firstName", "Zoraida"),
        ("lastName", "Villalobos-Etxeberria"),
        ("phone", "+34 611 987 654"),
        ("email", "zoraida.villalobos@sentinel.invalid"),
        ("location", "Villafranca del Sentinel"),
        ("province", "Provincia Centinela"),
        ("notes", "Nota confidencial sobre la candidata"),
        ("receivedAt", "2026-03-17"),
        ("consentAt", "2026-03-18"),
        ("reviewDueAt", "2027-03-17"),
    ];

    [Fact]
    public void A_candidate_write_logs_no_field_value()
    {
        using var sink = new InMemorySink();
        using var logger = Build(sink);

        foreach (var (property, value) in Sentinels)
        {
            // Logged under the field's own name, which is how a slice or a destructured
            // request would carry it.
            logger.ForContext(property, value).Information("Candidate write");
            logger.Information("Candidate write {@Field}", new Dictionary<string, object> { [property] = value });
        }

        AssertNoSentinel(sink);
    }

    [Fact]
    public void A_candidate_failure_logs_no_field_value()
    {
        using var sink = new InMemorySink();
        using var logger = Build(sink);

        foreach (var (property, value) in Sentinels)
        {
            logger.Error(
                new InvalidOperationException("fallo sintético"),
                "Candidate request failed {@Candidate}",
                Payload(property, value));
        }

        AssertNoSentinel(sink);
    }

    [Fact]
    public void A_candidate_field_nested_inside_a_destructured_request_is_still_redacted()
    {
        using var sink = new InMemorySink();
        using var logger = Build(sink);

        // The realistic failure mode: nobody logs `candidate.Email`, but somebody logs the
        // whole request object and a library destructures it.
        logger.Information(
            "Handling {@Request}",
            new Dictionary<string, object>
            {
                ["command"] = "candidate.update",
                ["payload"] = Payload("email", "zoraida.villalobos@sentinel.invalid"),
            });

        AssertNoSentinel(sink);
    }

    [Fact]
    public void Non_personal_properties_are_left_alone()
    {
        using var sink = new InMemorySink();
        using var logger = Build(sink);

        // Redaction that swallowed the identifiers would make the logs useless for the
        // thing they are for: correlating a change to an actor and a subject.
        logger.Information(
            "Candidate updated {CandidateId} {CorrelationId}",
            "01a08b17-58a0-777e-8e11-000000000000",
            "8ce2bbb29a684adbb9877e7ccb3e5682");

        var rendered = Rendered(sink);
        Assert.Contains("01a08b17-58a0-777e-8e11-000000000000", rendered, StringComparison.Ordinal);
        Assert.Contains("8ce2bbb29a684adbb9877e7ccb3e5682", rendered, StringComparison.Ordinal);
    }

    private static Dictionary<string, object> Payload(string property, string value) => new()
    {
        [property] = value,
        ["candidateId"] = "01a08b17-58a0-777e-8e11-000000000000",
    };

    private static void AssertNoSentinel(InMemorySink sink)
    {
        var rendered = Rendered(sink);
        foreach (var (property, value) in Sentinels)
        {
            Assert.DoesNotContain(value, rendered, StringComparison.Ordinal);
            _ = property;
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

    /// <summary>
    /// Captures log events for inspection. Hand-rolled rather than taken from a package:
    /// it is five lines, and a test-only dependency still has to be maintained.
    /// </summary>
    private sealed class InMemorySink : ILogEventSink, IDisposable
    {
        private readonly ConcurrentQueue<LogEvent> _events = new();

        public IEnumerable<LogEvent> LogEvents => _events;

        public void Emit(LogEvent logEvent) => _events.Enqueue(logEvent);

        public void Dispose() => _events.Clear();
    }
}
