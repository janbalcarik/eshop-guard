namespace EshopGuard.Core.Jev;

/// <summary>
/// The Jev API failed. <see cref="IsFatal"/> errors (missing or rejected key, no credit) stop the whole run;
/// other errors skip one segment.
/// </summary>
public sealed class JevApiException(string message, int statusCode, string? responseBody, bool isFatal, Exception? innerException = null)
    : Exception(message, innerException)
{
    /// <summary>HTTP status code, or 0 when there was no response.</summary>
    public int StatusCode { get; } = statusCode;

    /// <summary>Body of the error response, if any.</summary>
    public string? ResponseBody { get; } = responseBody;

    /// <summary>True for errors that would repeat for every request (401, 402, 403, missing key).</summary>
    public bool IsFatal { get; } = isFatal;
}
