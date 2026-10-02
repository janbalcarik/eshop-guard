using EshopGuard.Application.Problems;

namespace EshopGuard.Api.Problems;

/// <summary>A request body that checks itself before the service runs (fields missing, codes of fields).</summary>
public interface IValidatableRequest
{
    void Validate(ValidationResult result);
}

/// <summary>
/// The body of type <typeparamref name="T"/> must be present and valid: a missing body is <c>validation.failed</c> with
/// <c>errors.body = ["value.required"]</c>, its own checks add the codes of the fields. The services check again (they are the
/// rule); this only answers before any work starts.
/// </summary>
public sealed class ValidationEndpointFilter<T> : IEndpointFilter
    where T : class
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);
        var body = context.Arguments.OfType<T>().FirstOrDefault();
        var result = new ValidationResult();
        if (body is null)
        {
            result.Add("body", ProblemCodes.Fields.Required);
        }
        else if (body is IValidatableRequest validatable)
        {
            validatable.Validate(result);
        }

        result.ThrowIfInvalid();
        return await next(context).ConfigureAwait(false);
    }
}

public static class ValidationEndpointFilterExtensions
{
    public static RouteHandlerBuilder Validate<T>(this RouteHandlerBuilder builder)
        where T : class =>
        builder.AddEndpointFilter<ValidationEndpointFilter<T>>().ProducesProblemCodes(ProblemCodes.ValidationFailed);
}
