using EshopGuard.Application.Problems;

namespace EshopGuard.Application.RateLimits;

/// <summary>A limit of the sign-in: prefix of the key, capacity, its window and the scope reported in <c>params.scope</c>.</summary>
public sealed record AuthLimit(string Prefix, double Capacity, TimeSpan Window, string Scope)
{
    public double RefillPerSecond => Capacity / Window.TotalSeconds;
}

/// <summary>
/// Limits of the sign-in in <c>ops.rate_limit_buckets</c> (AD 5). The keys carry only an HMAC of the e-mail or the IP address
/// (or the id of a tenant), never a readable address: the table is global. A refused request gets <c>429 rate_limited</c>
/// with <c>params.scope</c> and <c>params.retryAfterSeconds</c>.
/// </summary>
public sealed class AuthRateLimits(IRateLimitBuckets buckets)
{
    public static readonly TimeSpan Hour = TimeSpan.FromHours(1);

    public static readonly AuthLimit LinkEmail = new("auth:link:email:", 5, Hour, "email_hourly");
    public static readonly AuthLimit LinkIp = new("auth:link:ip:", 20, Hour, "ip_hourly");
    public static readonly AuthLimit ResetEmail = new("auth:reset:email:", 5, Hour, "email_hourly");
    public static readonly AuthLimit ResetIp = new("auth:reset:ip:", 20, Hour, "ip_hourly");
    public static readonly AuthLimit PasswordIp = new("auth:password:ip:", 30, Hour, "ip_hourly");
    public static readonly AuthLimit InviteTenant = new("auth:invite:tenant:", 30, Hour, "tenant_hourly");

    /// <summary>Scope of the pause between two links of one e-mail address.</summary>
    public const string CooldownScope = "email_cooldown";

    /// <summary>Takes one token of <paramref name="limit"/> for <paramref name="hash"/>, or throws <c>429 rate_limited</c>.</summary>
    public async Task TakeAsync(AuthLimit limit, string hash, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(limit);
        var result = await buckets.TryTakeAsync(limit.Prefix + hash, 1, limit.Capacity, limit.RefillPerSecond, ct).ConfigureAwait(false);
        if (!result.Granted)
        {
            throw Limited(limit.Scope, result.RetryAfter);
        }
    }

    /// <summary><c>429 rate_limited</c> with the scope and the whole seconds to wait (at least 1).</summary>
    public static DomainException Limited(string scope, TimeSpan retryAfter) => new(ProblemCodes.RateLimited, 429, new Dictionary<string, object?>
    {
        ["scope"] = scope,
        ["retryAfterSeconds"] = retryAfter == TimeSpan.MaxValue ? 3600 : (int)Math.Ceiling(Math.Max(1, retryAfter.TotalSeconds)),
    });
}
