namespace EshopGuard.Jobs.Workers;

/// <summary>
/// Politeness state of a foreign domain kept in <c>ops.domains</c> between batches of all tenants (public data only):
/// robots.txt, Crawl-delay, sitemaps, request pace and errors.
/// </summary>
public sealed record DomainPolitenessState(
    string? RobotsTxt = null,
    DateTimeOffset? RobotsFetchedAt = null,
    int? CrawlDelayMs = null,
    IReadOnlyList<string>? Sitemaps = null,
    DateTimeOffset? LastRequestAt = null,
    double? Rate = null,
    int ConsecutiveErrors = 0,
    DateTimeOffset? BlockedUntil = null);
