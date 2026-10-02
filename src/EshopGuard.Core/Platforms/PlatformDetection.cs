namespace EshopGuard.Core.Platforms;

/// <summary>How sure the recognition is.</summary>
public enum PlatformConfidence
{
    /// <summary>At least two independent signatures (of different kinds) of one platform.</summary>
    Certain,

    /// <summary>One signature.</summary>
    Likely,

    /// <summary>No signature, or signatures of two platforms (fail-closed).</summary>
    Unknown,
}

/// <summary>Result of <see cref="PlatformDetector"/>: the platform (<c>unknown</c> when not sure), the confidence and the codes of every signal seen.</summary>
public sealed record PlatformDetection(string Platform, PlatformConfidence Confidence, IReadOnlyList<string> Signals)
{
    /// <summary>The code of a platform that was not recognized.</summary>
    public const string UnknownPlatform = "unknown";
}

/// <summary>
/// What the recognition reads of the downloaded home page: the final address, the headers and the cookie names of the response
/// and the bytes of the HTML. Texts of the page are never compared.
/// </summary>
public sealed record PlatformPage(Uri FinalUrl, IReadOnlyList<KeyValuePair<string, string>> Headers, IReadOnlyList<string> SetCookies, byte[] Body, string? Charset);
