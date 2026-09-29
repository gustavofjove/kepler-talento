namespace KeplerTalento.Application.Common.Errors;

public abstract class ApplicationExceptionBase(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}

public sealed class NotFoundException(string code, string message) : ApplicationExceptionBase(code, message);

public sealed class ForbiddenException(string code = "authorization.denied")
    : ApplicationExceptionBase(code, "No tiene permisos para realizar esta operación.");

public sealed class ConflictException(string code, string message) : ApplicationExceptionBase(code, message);

/// <summary>The request was well formed but its content cannot be processed (422).</summary>
public sealed class UnprocessableException(string code, string message) : ApplicationExceptionBase(code, message);

/// <summary>Too many requests of this kind are in flight; the caller may retry shortly (429).</summary>
public sealed class TooManyRequestsException(string code, string message) : ApplicationExceptionBase(code, message);

/// <summary>A dependency the operation needs is unavailable; the caller may retry (503).</summary>
public sealed class ServiceUnavailableException(string code, string message) : ApplicationExceptionBase(code, message);

public sealed record ValidationIssue(string Property, string Code, string Message);

public sealed class RequestValidationException(IReadOnlyCollection<ValidationIssue> issues)
    : ApplicationExceptionBase("validation.failed", "La solicitud contiene datos no válidos.")
{
    public IReadOnlyCollection<ValidationIssue> Issues { get; } = issues;
}
