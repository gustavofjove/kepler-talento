using System.Collections.Concurrent;
using KeplerTalento.Web.Observability;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Xunit;

namespace KeplerTalento.Tests.UnitTests.Features;

public sealed class IdentityLogRedactionTests
{
    private const string Email = "identity.sentinel@example.invalid";
    private const string DisplayName = "Identity Sentinel Person";
    private const string Subject = "4f33f13b-identity-subject-sentinel";
    private const string Token = "eyJhbGciOiJIUzI1NiJ9.eyJvaWQiOiJzZW50aW5lbCJ9.signatureSentinel";

    [Theory]
    [InlineData("authentication")]
    [InlineData("authorization")]
    [InlineData("administration-write")]
    public void Identity_data_and_credentials_never_reach_logs(string operation)
    {
        using var sink = new InMemorySink();
        using var logger = new LoggerConfiguration()
            .Enrich.With<PersonalDataRedactionEnricher>()
            .WriteTo.Sink(sink)
            .CreateLogger();

        logger.ForContext("email", Email)
            .ForContext("displayName", DisplayName)
            .ForContext("oid", Subject)
            .ForContext("authorization", $"Bearer {Token}")
            .Information("Identity operation {Operation} {@Request}", operation, new Dictionary<string, object>
            {
                ["externalSubject"] = Subject,
                ["accessToken"] = Token,
                ["email"] = Email,
                ["displayName"] = DisplayName,
            });

        var rendered = string.Join('\n', sink.Events.Select(value =>
            value.RenderMessage() + ' ' + string.Join(' ', value.Properties.Select(property => property.Value))));
        Assert.DoesNotContain(Email, rendered, StringComparison.Ordinal);
        Assert.DoesNotContain(DisplayName, rendered, StringComparison.Ordinal);
        Assert.DoesNotContain(Subject, rendered, StringComparison.Ordinal);
        Assert.DoesNotContain(Token, rendered, StringComparison.Ordinal);
        Assert.Contains(PersonalDataRedactionEnricher.Mask, rendered, StringComparison.Ordinal);
    }

    private sealed class InMemorySink : ILogEventSink, IDisposable
    {
        private readonly ConcurrentQueue<LogEvent> events = new();
        public IEnumerable<LogEvent> Events => events;
        public void Emit(LogEvent logEvent) => events.Enqueue(logEvent);
        public void Dispose() => events.Clear();
    }
}
