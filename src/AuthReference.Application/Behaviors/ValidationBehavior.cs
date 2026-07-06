using FluentValidation;
using MediatR;

namespace AuthReference.Application.Behaviors;

/// <summary>
/// MediatR pipeline behavior: runs every registered <see cref="IValidator{T}"/>
/// against an incoming request before the handler sees it. Throws
/// <see cref="ValidationException"/> on first failure — the API layer maps that
/// to <c>400 Bad Request</c>.
/// </summary>
public sealed class ValidationBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IReadOnlyList<IValidator<TRequest>> _validators;

    public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators)
    {
        _validators = validators.ToList();
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (_validators.Count == 0) return await next();

        var context = new ValidationContext<TRequest>(request);
        var failures = new List<FluentValidation.Results.ValidationFailure>();

        foreach (var validator in _validators)
        {
            var result = await validator.ValidateAsync(context, cancellationToken);
            if (!result.IsValid) failures.AddRange(result.Errors);
        }

        if (failures.Count > 0) throw new ValidationException(failures);

        return await next();
    }
}
