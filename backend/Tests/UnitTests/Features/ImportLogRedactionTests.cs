using System.Collections.Concurrent;
using KeplerTalento.Web.Observability;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Xunit;

namespace KeplerTalento.Tests.UnitTests.Features;

/// <summary>
/// KTL-17. Imported values and the uploaded file's name must not reach a log, whatever property
/// they are logged under — the import file's own column names included.
/// </summary>
public sealed class ImportLogRedactionTests
{
    private static readonly (string Property, string Value)[] Sentinels =
    [
        ("first_name", "Zoraida"),
        ("last_name", "Villalobos-Etxeberria"),
        ("email", "zoraida.villalobos@sentinel.invalid"),
        ("received_at", "2026-03-17"),
        ("consent_at", "2026-03-18"),
        ("review_due_at", "2027-03-17"),
        ("country", "Pais Centinela"),
        ("availability", "Disponibilidad centinela"),
        ("source", "Fuente centinela"),
        ("languages", "Klingon-centinela:B2"),
        ("originalFileName", "candidatos-zoraida-villalobos.csv"),
        ("fileName", "candidatos-zoraida-villalobos.csv"),
        ("file_name", "candidatos-zoraida-villalobos.csv"),
        ("storageKey", "imports/0123456789abcdef0123456789abcdef/content"),
    ];

    [Fact]
    public void An_import_row_logged_under_its_column_names_reveals_no_value()
    {
        using var sink = new InMemorySink();
        using var logger = Build(sink);

        foreach (var (property, value) in Sentinels)
        {
            logger.ForContext(property, value).Information("Import row evaluated");
            logger.Warning("Import row {@Row}", new Dictionary<string, object> { [property] = value, ["rowNumber"] = 7 });
        }

        var rendered = Rendered(sink);
        foreach (var (_, value) in Sentinels)
        {
            Assert.DoesNotContain(value, rendered, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void The_safe_import_metadata_stays_readable()
    {
        using var sink = new InMemorySink();
        using var logger = Build(sink);

        logger.Information(
            "Import batch {BatchId} row {RowNumber} {RowOutcome} on {Field} with {ReasonCode}",
            "01a08b17-58a0-777e-8e11-000000000000",
            47,
            "Rejected",
            "email",
            "email.invalid");

        var rendered = Rendered(sink);
        Assert.Contains("01a08b17-58a0-777e-8e11-000000000000", rendered, StringComparison.Ordinal);
        Assert.Contains("47", rendered, StringComparison.Ordinal);
        Assert.Contains("email.invalid", rendered, StringComparison.Ordinal);
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
