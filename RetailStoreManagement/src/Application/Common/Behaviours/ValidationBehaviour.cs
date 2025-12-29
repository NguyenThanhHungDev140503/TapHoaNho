using FluentValidation;
using MediatR;

namespace Application.Common.Behaviours;

/// <summary>
/// Pipeline Behaviour tự động validate requests trước khi chúng được xử lý bởi Handler
/// </summary>
/// <typeparam name="TRequest">Loại request</typeparam>
/// <typeparam name="TResponse">Loại response</typeparam>
public class ValidationBehaviour<TRequest, TResponse> 
    : IPipelineBehavior<TRequest, TResponse> 
    where TRequest : notnull
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;
    
    public ValidationBehaviour(IEnumerable<IValidator<TRequest>> validators)
    {
        _validators = validators;
    }
    
    public async Task<TResponse> Handle(
        TRequest request, 
        RequestHandlerDelegate<TResponse> next, 
        CancellationToken cancellationToken)
    {
        if (_validators.Any())
        {
            var context = new ValidationContext<TRequest>(request);

            var validationResults = await Task.WhenAll(
                _validators.Select(v => v.ValidateAsync(context, cancellationToken)));

            var failures = validationResults
                .Where(r => !r.IsValid)
                .SelectMany(r => r.Errors)
                .ToList();

            if (failures.Count > 0)
                throw new Exceptions.ValidationException(failures);
        }
        
        return await next();
    }
}
