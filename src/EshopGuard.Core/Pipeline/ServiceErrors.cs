using EshopGuard.Core.Fix;
using EshopGuard.Core.Jev;

namespace EshopGuard.Core.Pipeline;

/// <summary>
/// Which failures of a paid service (Jev, OpenAI) are worth asking again later: no answer (network, timeout), 408, 429 and
/// 5xx. The clients repeat these themselves first; an item that still failed is counted as temporary, so a worker can
/// repeat its batch (answers already received are in the cache and are not paid again). Fatal errors (key, credit) stop the
/// batch elsewhere; other errors (400, a refused or malformed answer) would fail again and are not repeated.
/// </summary>
public static class ServiceErrors
{
    /// <summary>True for an HTTP status (0 = no answer) that may pass on a later attempt.</summary>
    public static bool IsTransientStatus(int status) => status is 0 or 408 or 429 || status >= 500;

    /// <summary>True for a failure of a service call that may pass on a later attempt.</summary>
    public static bool IsTransient(Exception exception) => exception switch
    {
        JevApiException jev => !jev.IsFatal && IsTransientStatus(jev.StatusCode),
        RewriteApiException rewrite => !rewrite.IsFatal && IsTransientStatus(rewrite.StatusCode),
        HttpRequestException or TimeoutException => true,
        TaskCanceledException { InnerException: TimeoutException } => true,
        _ => false,
    };
}
