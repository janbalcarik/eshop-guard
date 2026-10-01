namespace EshopGuard.Data.Entities.Ref;

/// <summary>Values of <c>MarketWebStatus</c> (stored as snake_case text with a CHECK constraint).</summary>
public enum MarketWebStatus
{
    Hidden,
    Preview,
    Live,
}

/// <summary>Values of <c>MarketChecksStatus</c> (stored as snake_case text with a CHECK constraint).</summary>
public enum MarketChecksStatus
{
    None,
    Limited,
    Full,
}
