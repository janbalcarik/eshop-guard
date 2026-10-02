using EshopGuard.Api.Auth;
using Microsoft.AspNetCore.Routing;

namespace EshopGuard.Api.Tenancy;

/// <summary>
/// At start, every endpoint must carry its policies (AD 6, fail-closed): one under <c>/api/t/</c> its lowest role
/// (<see cref="TenantRoleMetadata"/>), one that changes data (POST, PUT, PATCH, DELETE) the CSRF check, unless it is under
/// <c>/api/webhooks/</c> and turns it off. Otherwise the API does not start and the error names the endpoints.
/// </summary>
public sealed class EndpointPolicyValidator(EndpointDataSource endpoints) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        var problems = Validate(endpoints);
        return problems.Count == 0
            ? Task.CompletedTask
            : throw new InvalidOperationException("config.endpoint_policy_missing: " + string.Join("; ", problems));
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>Endpoints without their policy, as <c>METHOD /pattern: what is missing</c>.</summary>
    public static IReadOnlyList<string> Validate(EndpointDataSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var problems = new List<string>();
        foreach (var endpoint in source.Endpoints.OfType<RouteEndpoint>())
        {
            var pattern = "/" + (endpoint.RoutePattern.RawText ?? string.Empty).TrimStart('/');
            if (!pattern.StartsWith("/api/", StringComparison.Ordinal))
            {
                continue;
            }

            var methods = endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? [];
            var name = $"{string.Join(",", methods)} {pattern}";
            if (pattern.StartsWith("/api/t/", StringComparison.Ordinal) && endpoint.Metadata.GetMetadata<TenantRoleMetadata>() is null)
            {
                problems.Add(name + ": RequireTenantRole");
            }

            var unsafeMethod = methods.Count == 0 || methods.Any(CsrfEndpointFilter.IsUnsafe);
            if (!unsafeMethod)
            {
                continue;
            }

            var disabled = endpoint.Metadata.GetMetadata<DisableCsrfMetadata>() is not null;
            if (disabled && !pattern.StartsWith("/api/webhooks/", StringComparison.Ordinal))
            {
                problems.Add(name + ": DisableCsrf outside /api/webhooks/");
            }
            else if (!disabled && endpoint.Metadata.GetMetadata<CsrfProtectedMetadata>() is null)
            {
                problems.Add(name + ": RequireCsrf");
            }
        }

        return problems;
    }
}
