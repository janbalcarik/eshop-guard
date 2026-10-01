namespace EshopGuard.Data.Entities.Usage;

/// <summary>Values of <c>UsageProvider</c> (stored as snake_case text with a CHECK constraint).</summary>
public enum UsageProvider
{
    Jev,
    Openai,
    Crawl,
}

/// <summary>Values of <c>UsageOperation</c> (stored as snake_case text with a CHECK constraint).</summary>
public enum UsageOperation
{
    SentenceEval,
    Sieve,
    Profile,
    Rewrite,
    Recheck,
    Fetch,
}
