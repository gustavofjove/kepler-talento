namespace KeplerTalento.Application.Common.Errors;

public abstract class ApplicationExceptionBase(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}

public sealed class NotFoundException(string code, string message) : ApplicationExceptionBase(code, message);

public sealed class ForbiddenException(string code = "authorization.denied")
    : ApplicationExceptionBase(code, "No tiene permisos para realizar esta operación.");

public sealed class ConflictException(string code, string message) : ApplicationExceptionBase(code, message);

public sealed record ValidationIssue(string Property, string Code, string Message);

public sealed class RequestValidationException(IReadOnlyCollection<ValidationIssue> issues)
    : ApplicationExceptionBase("validation.failed", "La solicitud contiene datos no válidos.")
{
    public IReadOnlyCollection<ValidationIssue> Issues { get; } = issues;
}
