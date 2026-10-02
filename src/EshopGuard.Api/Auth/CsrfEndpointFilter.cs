using EshopGuard.Api.Problems;
using EshopGuard.Application.Problems;
using Microsoft.AspNetCore.Antiforgery;

namespace EshopGuard.Api.Auth;

/// <summary>Marks an endpoint protected by <see cref="CsrfEndpointFilter"/>.</summary>
public sealed class CsrfProtectedMetadata;

/// <summary>Turns the check off; allowed only under <c>/api/webhooks/</c> (<see cref="Tenancy.EndpointPolicyValidator"/>).</summary>
public sealed class DisableCsrfMetadata;

/// <summary>
/// Every POST, PUT, PATCH and DELETE under <c>/api</c> carries a valid <c>X-CSRF-TOKEN</c> (AD 7), also before sign-in
/// (a forged sign-in). A missing or invalid token is <c>400 csrf.invalid</c> and nothing runs.
/// </summary>
public sealed class CsrfEndpointFilter(IAntiforgery antiforgery) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);
        var http = context.HttpContext;
        if (IsUnsafe(http.Request.Method) && http.GetEndpoint()?.Metadata.GetMetadata<DisableCsrfMetadata>() is null)
        {
            try
            {
                await antiforgery.ValidateRequestAsync(http).ConfigureAwait(false);
            }
            catch (AntiforgeryValidationException)
            {
                return EgProblem.Result(http, ProblemCodes.CsrfInvalid, StatusCodes.Status400BadRequest);
            }
        }

        return await next(context).ConfigureAwait(false);
    }

    public static bool IsUnsafe(string method) =>
        HttpMethods.IsPost(method) || HttpMethods.IsPut(method) || HttpMethods.IsPatch(method) || HttpMethods.IsDelete(method);
}

public static class CsrfExtensions
{
    /// <summary>Protects every endpoint of the group (the filter checks only unsafe methods).</summary>
    public static RouteGroupBuilder RequireCsrf(this RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);
        group.AddEndpointFilter<CsrfEndpointFilter>();
        group.WithMetadata(new CsrfProtectedMetadata());
        group.ProducesProblemCodes(ProblemCodes.CsrfInvalid);
        return group;
    }

    /// <summary>No CSRF check (webhooks of payments and platforms, change 12).</summary>
    public static TBuilder DisableCsrf<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder =>
        builder.WithMetadata(new DisableCsrfMetadata());
}
