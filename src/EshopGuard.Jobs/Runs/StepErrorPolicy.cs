using EshopGuard.Core.Fix;
using EshopGuard.Core.Jev;
using EshopGuard.Core.Pipeline;
using EshopGuard.Core.Rules;
using EshopGuard.Data.Entities.Checks;
using EshopGuard.Jobs.Queue;
using Npgsql;

namespace EshopGuard.Jobs.Runs;

/// <summary>Kinds of errors of a step (design of change 8, "Chyby a samooprava").</summary>
public enum StepErrorKind
{
    /// <summary>Jev, OpenAI or the e-shop failed for a while (no answer, 408, 429, 5xx): repeated with a growing delay.</summary>
    Transient,

    /// <summary>The service refuses every request (key, credit): the class of jobs is paused without using an attempt.</summary>
    Fatal,

    /// <summary>Database or file store: nothing is committed and the queue repeats the job.</summary>
    Infrastructure,

    /// <summary>One item failed for good (404/410, too large, internal network): recorded with its code, not repeated.</summary>
    PermanentItem,

    /// <summary>The run cannot go on (invalid rule sets): it ends <c>failed</c> with a code.</summary>
    PermanentRun,

    /// <summary>Anything else: the queue repeats the job; when its attempts are used up, the run ends <c>failed</c>.</summary>
    Unexpected,
}

/// <summary>
/// The rules of errors of the steps of a run. A batch of Jev or OpenAI with items that failed temporarily is repeated by
/// the queue (the answers it already has are in the cache and are not paid again) until its last attempt; the last attempt
/// keeps what it got and the rest is reported (<c>partial</c>). A job whose attempts are used up never leaves its run
/// unfinished: the run ends <c>failed</c> (fail-closed).
/// </summary>
public static class StepErrorPolicy
{
    /// <summary>Code of a batch repeated because Jev did not answer some of its items.</summary>
    public const string JevUnavailable = "jev.unavailable";

    /// <summary>Code of a batch repeated because OpenAI did not answer some of its pages.</summary>
    public const string LlmUnavailable = "llm.unavailable";

    /// <summary>An error of Jev or OpenAI by its HTTP status (0 = no answer).</summary>
    public static StepErrorKind OfService(int status, bool fatal) =>
        fatal ? StepErrorKind.Fatal : ServiceErrors.IsTransientStatus(status) ? StepErrorKind.Transient : StepErrorKind.PermanentItem;

    /// <summary>
    /// An answer of the e-shop by its HTTP status: 408, 429 and 5xx are temporary (the crawl slows down and asks again),
    /// the rest (404, 410, 403…) is final for the page.
    /// </summary>
    public static StepErrorKind OfShop(int status) =>
        status is 408 or 429 || status >= 500 ? StepErrorKind.Transient : StepErrorKind.PermanentItem;

    /// <summary>An exception thrown out of a step.</summary>
    public static StepErrorKind Of(Exception exception) => exception switch
    {
        JevApiException jev => OfService(jev.StatusCode, jev.IsFatal),
        RewriteApiException rewrite => OfService(rewrite.StatusCode, rewrite.IsFatal),
        TransientStepException => StepErrorKind.Transient,
        RuleValidationException => StepErrorKind.PermanentRun,
        NpgsqlException or IOException => StepErrorKind.Infrastructure,
        _ => StepErrorKind.Unexpected,
    };

    /// <summary>The state of an address whose download failed after the crawl's own repeats.</summary>
    public static RunUrlState OfShopFailure(int? status, string? failureCode) => (status, failureCode) switch
    {
        (404 or 410, _) => RunUrlState.Gone,
        (_, "too_large") => RunUrlState.TooLarge,
        _ => RunUrlState.Failed,
    };

    /// <summary>True when a batch with items that failed temporarily goes back to the queue (it is not its last attempt).</summary>
    public static bool RepeatBatch(int transientErrors, ClaimedJob job)
    {
        ArgumentNullException.ThrowIfNull(job);
        return transientErrors > 0 && !IsLastAttempt(job);
    }

    /// <summary>True on the last attempt of a job.</summary>
    public static bool IsLastAttempt(ClaimedJob job)
    {
        ArgumentNullException.ThrowIfNull(job);
        return job.Attempt >= job.MaxAttempts;
    }
}
