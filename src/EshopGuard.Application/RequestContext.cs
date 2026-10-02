using System.Net;

namespace EshopGuard.Application;

/// <summary>
/// What the services need to know about the current request (filled by the API, empty in the worker): the client address
/// (only ever stored as an HMAC), <c>Accept-Language</c> and the trace id.
/// </summary>
public sealed class RequestContext
{
    public IPAddress? Ip { get; set; }

    public string? AcceptLanguage { get; set; }

    public string? TraceId { get; set; }
}
