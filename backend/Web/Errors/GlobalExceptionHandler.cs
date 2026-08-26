using KeplerTalento.Application.Common.Errors;
using KeplerTalento.Web.Correlation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace KeplerTalento.Web.Errors;

public sealed class GlobalExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (status, title, code) = exception switch
        {
            RequestValidationException validation => (StatusCodes.Status400BadRequest, "Solicitud no válida", validation.Code),
            ForbiddenException forbidden => (StatusCodes.Status403Forbidden, "Acceso denegado", forbidden.Code),
            NotFoundException notFound => (StatusCodes.Status404NotFound, "Recurso no encontrado", notFound.Code),
            ConflictException conflict => (StatusCodes.Status409Conflict, "Conflicto de concurrencia", conflict.Code),
            _ => (StatusCodes.Status500InternalServerError, "Error inesperado", "server.unexpected"),
        };
        if (status >= 500)
        {
            logger.LogError(exception, "Unhandled request failure with code {ErrorCode}", code);
        }
        var problem = new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = status >= 500 ? "Se ha producido un error inesperado." : exception.Message,
            Instance = httpContext.Request.Path,
        };
        problem.Extensions["code"] = code;
        var correlationId = httpContext.RequestServices.GetRequiredService<CorrelationContext>().CorrelationId;
        httpContext.Response.Headers[CorrelationMiddleware.HeaderName] = correlationId;
        problem.Extensions["correlationId"] = correlationId;
        if (exception is RequestValidationException validationException)
        {
            problem.Extensions["errors"] = validationException.Issues;
        }
        httpContext.Response.StatusCode = status;
        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
            Exception = exception,
        });
    }
}
