using FluentValidation;

namespace ZnunyStats.Api.Common.Validation;

/// <summary>
/// Valida el argumento <typeparamref name="T"/> de cualquier endpoint del grupo. Si no es válido responde 400
/// con <c>errors</c> por campo y el primer mensaje en <c>detail</c>, que es lo que muestra el dashboard.
/// </summary>
public sealed class ValidationFilter<T>(IValidator<T> validator) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        if (context.Arguments.OfType<T>().FirstOrDefault() is { } argument)
        {
            var result = await validator.ValidateAsync(argument, context.HttpContext.RequestAborted);
            if (!result.IsValid)
            {
                return Results.ValidationProblem(result.ToDictionary(), detail: result.Errors[0].ErrorMessage);
            }
        }

        return await next(context);
    }
}
