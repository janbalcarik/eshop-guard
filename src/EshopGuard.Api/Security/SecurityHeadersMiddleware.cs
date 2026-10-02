namespace EshopGuard.Api.Security;

/// <summary>
/// Headers of every answer of the API: <c>Strict-Transport-Security</c> (outside Development), <c>X-Content-Type-Options:
/// nosniff</c>, <c>Referrer-Policy: no-referrer</c>, <c>Cache-Control: no-store</c> for <c>/api</c> (answers carry personal
/// data) unless the endpoint set its own (only the catalog of rule texts, which holds no personal data, change 11), and
/// <c>X-Frame-Options: DENY</c>.
/// </summary>
public sealed class SecurityHeadersMiddleware(RequestDelegate next, IHostEnvironment environment)
{
    public Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.Response.OnStarting(() =>
        {
            var headers = context.Response.Headers;
            headers.XContentTypeOptions = "nosniff";
            headers["Referrer-Policy"] = "no-referrer";
            headers.XFrameOptions = "DENY";
            if (!environment.IsDevelopment())
            {
                headers.StrictTransportSecurity = "max-age=31536000; includeSubDomains";
            }

            if (context.Request.Path.StartsWithSegments("/api", StringComparison.Ordinal) && string.IsNullOrEmpty(headers.CacheControl))
            {
                headers.CacheControl = "no-store";
            }

            return Task.CompletedTask;
        });
        return next(context);
    }
}
