namespace EshopGuard.Data.Entities.Shops;

/// <summary>Values of <c>ShopPlatform</c> (stored as snake_case text with a CHECK constraint).</summary>
public enum ShopPlatform
{
    Shoptet,
    Upgates,
    Biznisweb,
    Woocommerce,
    Shopify,
    Other,
    Unknown,
}

/// <summary>Values of <c>ShopSourceMode</c> (stored as snake_case text with a CHECK constraint).</summary>
public enum ShopSourceMode
{
    Connector,
    Feed,
    Web,
}

/// <summary>Values of <c>ShopStatus</c> (stored as snake_case text with a CHECK constraint).</summary>
public enum ShopStatus
{
    Draft,
    Sample,
    AwaitingPayment,
    Analyzing,
    Active,
    Paused,
    Canceled,
}

/// <summary>Values of <c>ShopMarketStatus</c> (stored as snake_case text with a CHECK constraint).</summary>
public enum ShopMarketStatus
{
    Suggested,
    Active,
    Declined,
    Unsupported,
}

/// <summary>Values of <c>EvidenceLevel</c> (stored as snake_case text with a CHECK constraint).</summary>
public enum EvidenceLevel
{
    Strong,
    Delivery,
    Generic,
}

/// <summary>Values of <c>MarketSource</c> (stored as snake_case text with a CHECK constraint).</summary>
public enum MarketSource
{
    Detected,
    User,
}

/// <summary>Values of <c>LanguageSwitchMethod</c> (stored as snake_case text with a CHECK constraint).</summary>
public enum LanguageSwitchMethod
{
    Path,
    Subdomain,
    Domain,
    Query,
    Cookie,
    AcceptLanguage,
    Script,
    BrowserTranslation,
}

/// <summary>Values of <c>LanguageSource</c> (stored as snake_case text with a CHECK constraint).</summary>
public enum LanguageSource
{
    Main,
    Hreflang,
    Switcher,
    Connector,
    Llm,
    User,
}

/// <summary>Values of <c>ShopLanguageStatus</c> (stored as snake_case text with a CHECK constraint).</summary>
public enum ShopLanguageStatus
{
    Active,
    Excluded,
    NeedsConfirmation,
    Unsupported,
    NeedsBrowser,
    Mismatch,
}

/// <summary>Values of <c>VerificationMethod</c> (stored as snake_case text with a CHECK constraint).</summary>
public enum VerificationMethod
{
    Meta,
    Dns,
    Connector,
}

/// <summary>Values of <c>ShopVerificationStatus</c> (stored as snake_case text with a CHECK constraint).</summary>
public enum ShopVerificationStatus
{
    Pending,
    Verified,
    Failed,
}

/// <summary>Values of <c>ConnectorStatus</c> (stored as snake_case text with a CHECK constraint).</summary>
public enum ConnectorStatus
{
    Connected,
    Error,
    Revoked,
    Paused,
}

/// <summary>Values of <c>ConnectorAccess</c> (stored as snake_case text with a CHECK constraint).</summary>
public enum ConnectorAccess
{
    Read,
    ReadWrite,
}

/// <summary>Values of <c>FeedFormat</c> (stored as snake_case text with a CHECK constraint).</summary>
public enum FeedFormat
{
    Heureka,
    Google,
}
