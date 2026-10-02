using EshopGuard.Application.Problems;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Diagnostics;

namespace EshopGuard.Api.Problems;

/// <summary>
/// Turns exceptions into <see cref="EgProblemDto"/> (AD 12): a <see cref="DomainException"/> into its code; a bad request
/// (unreadable JSON, a body too large) into <c>request.invalid</c>; a failed antiforgery check into <c>csrf.invalid</c>;
/// anything else into <c>500 internal_error</c> with the trace id only, never the text of the exception.
/// </summary>
internal sealed partial class EgExceptionHandler(ILogger<EgExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var problem = exception switch
        {
            DomainException domain => EgProblem.From(httpContext, domain),
            AntiforgeryValidationException => EgProblem.Create(httpContext, ProblemCodes.CsrfInvalid, StatusCodes.Status400BadRequest),
            BadHttpRequestException bad => EgProblem.Create(httpContext, ProblemCodes.RequestInvalid, bad.StatusCode),
            Billing.Stripe.BillingUnavailableException => EgProblem.Create(httpContext, ProblemCodes.BillingUnavailable, StatusCodes.Status503ServiceUnavailable),
            Billing.Stripe.StripeGatewayException stripe => Unavailable(httpContext, stripe),
            _ => null,
        };
        if (problem is null)
        {
            LogUnexpected(logger, exception, httpContext.TraceIdentifier);
            problem = EgProblem.Create(httpContext, ProblemCodes.InternalError, StatusCodes.Status500InternalServerError);
        }

        await EgProblem.WriteAsync(httpContext, problem).ConfigureAwait(false);
        return true;
    }

    /// <summary>Stripe refused or did not answer: <c>503 billing.unavailable</c>, the code of Stripe only in the log.</summary>
    private EgProblemDto Unavailable(HttpContext context, Billing.Stripe.StripeGatewayException exception)
    {
        LogStripe(logger, exception.Code, exception.HttpStatus, context.TraceIdentifier);
        return EgProblem.Create(context, ProblemCodes.BillingUnavailable, StatusCodes.Status503ServiceUnavailable);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "stripe.failed {Code} {HttpStatus} {TraceId}")]
    private static partial void LogStripe(ILogger logger, string code, int? httpStatus, string traceId);

    [LoggerMessage(Level = LogLevel.Error, Message = "request.failed {TraceId}")]
    private static partial void LogUnexpected(ILogger logger, Exception exception, string traceId);
}
