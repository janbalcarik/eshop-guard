using System.Threading.RateLimiting;
using EshopGuard.Api.Problems;
using EshopGuard.Api.Tenancy;
using EshopGuard.Application.Problems;

namespace EshopGuard.Api.RateLimiting;

/// <summary>
/// The first sieve against flooding, in memory of one instance (AD 5; the rules of the specification are the buckets in the
/// database): <c>/api/auth/*</c> <c>RateLimiting:AuthPerMinute</c> requests a minute per IP (proposal 30), everything else
/// <c>RateLimiting:PerMinute</c> per user or IP (proposal 300). A refusal is <c>429 rate_limited</c> (<c>scope = burst</c>).
/// </summary>
public static class RateLimiterSetup
{
    public const string AuthPolicy = "auth";

    public static IServiceCollection AddEshopGuardRateLimiter(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var authPerMinute = configuration.GetValue("RateLimiting:AuthPerMinute", 30);
        var perMinute = configuration.GetValue("RateLimiting:PerMinute", 300);
        services.AddRateLimiter(o =>
        {
            o.AddPolicy(AuthPolicy, context => RateLimitPartition.GetFixedWindowLimiter(
                "ip:" + (context.Connection.RemoteIpAddress?.ToString() ?? "unknown"),
                _ => Window(authPerMinute)));
            o.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                context.Request.Path.StartsWithSegments("/api/auth", StringComparison.Ordinal)
                    ? RateLimitPartition.GetNoLimiter("auth")
                    : RateLimitPartition.GetFixedWindowLimiter(
                        context.User.UserId() is { } user ? "user:" + user.ToString("N") : "ip:" + (context.Connection.RemoteIpAddress?.ToString() ?? "unknown"),
                        _ => Window(perMinute)));
            o.OnRejected = async (context, ct) =>
            {
                var retry = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var after) ? (int)Math.Ceiling(after.TotalSeconds) : 60;
                await EgProblem.WriteAsync(context.HttpContext, EgProblem.Create(context.HttpContext, ProblemCodes.RateLimited,
                    StatusCodes.Status429TooManyRequests, new Dictionary<string, object?> { ["scope"] = "burst", ["retryAfterSeconds"] = Math.Max(1, retry) }))
                    .ConfigureAwait(false);
            };
        });
        return services;
    }

    private static FixedWindowRateLimiterOptions Window(int permits) => new()
    {
        PermitLimit = permits,
        Window = TimeSpan.FromMinutes(1),
        QueueLimit = 0,
        AutoReplenishment = true,
    };
}
