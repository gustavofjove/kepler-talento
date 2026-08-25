using System.Text.RegularExpressions;
using Serilog.Context;

namespace KeplerTalento.Web.Correlation;

public sealed partial class CorrelationMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-Correlation-ID";
    private const int MaximumLength = 100;

    public async Task InvokeAsync(HttpContext httpContext, CorrelationContext correlation)
    {
        var supplied = httpContext.Request.Headers[HeaderName].ToString();
        correlation.CorrelationId = IsValid(supplied) ? supplied : Guid.NewGuid().ToString("N");
        httpContext.TraceIdentifier = correlation.CorrelationId;
        httpContext.Response.Headers[HeaderName] = correlation.CorrelationId;
        using (LogContext.PushProperty("CorrelationId", correlation.CorrelationId))
        {
            await next(httpContext);
        }
    }

    private static bool IsValid(string value) =>
        value.Length is > 0 and <= MaximumLength && ValidCorrelationRegex().IsMatch(value);

    [GeneratedRegex("^[A-Za-z0-9._:-]+$", RegexOptions.CultureInvariant)]
    private static partial Regex ValidCorrelationRegex();
}
