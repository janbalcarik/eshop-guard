namespace EshopGuard.Data.Entities.Content;

/// <summary>Values of <c>PageType</c> (stored as snake_case text with a CHECK constraint).</summary>
public enum PageType
{
    Home,
    Product,
    Category,
    Legal,
    Content,
}

/// <summary>Values of <c>PageSource</c> (stored as snake_case text with a CHECK constraint).</summary>
public enum PageSource
{
    Crawl,
    Connector,
    Feed,
}

/// <summary>Values of <c>PageStatus</c> (stored as snake_case text with a CHECK constraint).</summary>
public enum PageStatus
{
    Active,
    Gone,
    RobotsBlocked,
    NotLoaded,
    Error,
}
