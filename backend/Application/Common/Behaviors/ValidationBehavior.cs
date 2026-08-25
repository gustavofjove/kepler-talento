using FluentValidation;
using KeplerTalento.Application.Common.Errors;
using MediatR;

namespace KeplerTalento.Application.Common.Behaviors;

public sealed class ValidationBehavior<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var failures = new List<ValidationIssue>();
        foreach (var validator in validators)
        {
            var result = await validator.ValidateAsync(request, cancellationToken);
            failures.AddRange(result.Errors.Select(error => new ValidationIssue(
                error.PropertyName,
                string.IsNullOrWhiteSpace(error.ErrorCode) ? "validation.invalid" : error.ErrorCode,
                error.ErrorMessage)));
        }
        if (failures.Count > 0)
        {
            throw new RequestValidationException(failures);
        }
        return await next(cancellationToken);
    }
}
