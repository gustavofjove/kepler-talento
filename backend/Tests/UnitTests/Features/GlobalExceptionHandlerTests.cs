using System.Text.Json;
using KeplerTalento.Application.Common.Errors;
using KeplerTalento.Web.Correlation;
using KeplerTalento.Web.Errors;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace KeplerTalento.Tests.UnitTests.Features;

/// <summary>
/// The refusal types added by KTL-32 map to their status, keep their Spanish message and stable
/// code, and are not logged as unhandled failures.
/// </summary>
public sealed class GlobalExceptionHandlerTests
{
    [Theory]
    [InlineData("unprocessable", 422, null)]
    [InlineData("busy", 429, "5")]
    [InlineData("unavailable", 503, "30")]
    public async Task A_known_refusal_maps_to_its_status_code_message_and_retry_hint(string kind, int status, string? retryAfter)
    {
        Exception exception = kind switch
        {
            "unprocessable" => new UnprocessableException("cv_draft.unreadable", "No se ha podido procesar el CV."),
            "busy" => new TooManyRequestsException("cv_draft.busy", "Hay demasiados CV en proceso."),
            _ => new ServiceUnavailableException("cv_draft.scanner_unavailable", "No se puede analizar el archivo."),
        };
        var logger = new CountingLogger();
        var (context, body) = Context();

        var handled = await new GlobalExceptionHandler(context.RequestServices.GetRequiredService<IProblemDetailsService>(), logger)
            .TryHandleAsync(context, exception, CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(status, context.Response.StatusCode);
        Assert.Equal(retryAfter, context.Response.Headers.RetryAfter.FirstOrDefault());
        var problem = JsonDocument.Parse(body.ToArray()).RootElement;
        Assert.Equal(((ApplicationExceptionBase)exception).Code, problem.GetProperty("code").GetString());
        Assert.Equal(exception.Message, problem.GetProperty("detail").GetString());
        Assert.Equal(0, logger.Errors);
    }

    [Fact]
    public async Task An_unexpected_failure_still_hides_its_message_and_is_logged()
    {
        var logger = new CountingLogger();
        var (context, body) = Context();

        await new GlobalExceptionHandler(context.RequestServices.GetRequiredService<IProblemDetailsService>(), logger)
            .TryHandleAsync(context, new InvalidOperationException("internal detail"), CancellationToken.None);

        Assert.Equal(500, context.Response.StatusCode);
        Assert.DoesNotContain("internal detail", System.Text.Encoding.UTF8.GetString(body.ToArray()), StringComparison.Ordinal);
        Assert.Equal(1, logger.Errors);
    }

    private static (DefaultHttpContext Context, MemoryStream Body) Context()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddProblemDetails();
        services.AddScoped<CorrelationContext>();
        var body = new MemoryStream();
        var context = new DefaultHttpContext { RequestServices = services.BuildServiceProvider() };
        context.Request.Path = "/api/candidates/draft-from-document";
        context.Response.Body = body;
        return (context, body);
    }

    private sealed class CountingLogger : ILogger<GlobalExceptionHandler>
    {
        public int Errors { get; private set; }

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => NullLogger.Instance.BeginScope(state);

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (logLevel >= LogLevel.Error)
            {
                Errors++;
            }
        }
    }
}
