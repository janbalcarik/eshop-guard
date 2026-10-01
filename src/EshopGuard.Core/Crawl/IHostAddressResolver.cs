using System.Net;

namespace EshopGuard.Core.Crawl;

/// <summary>Translates a host name to its addresses before the crawler connects (tests replace it).</summary>
public interface IHostAddressResolver
{
    /// <summary>All addresses of the host; an IP literal resolves to itself.</summary>
    Task<IPAddress[]> ResolveAsync(string host, CancellationToken ct);
}

/// <summary>The system DNS.</summary>
internal sealed class DnsHostAddressResolver : IHostAddressResolver
{
    public Task<IPAddress[]> ResolveAsync(string host, CancellationToken ct) =>
        IPAddress.TryParse(host.Trim('[', ']'), out var literal) ? Task.FromResult(new[] { literal }) : Dns.GetHostAddressesAsync(host, ct);
}

/// <summary>
/// Connects the crawler's HTTP client only to checked addresses: the host is resolved once, when any of its addresses is
/// blocked (<see cref="SsrfGuard"/>) no connection is made, otherwise the socket connects to exactly those addresses, so
/// a DNS change between the check and the connection (DNS rebinding) cannot reach an internal address.
/// </summary>
internal static class SsrfConnector
{
    public static async ValueTask<Stream> ConnectAsync(IHostAddressResolver resolver, bool allowPrivateNetwork, System.Net.Http.SocketsHttpConnectionContext context, CancellationToken ct)
    {
        var endPoint = context.DnsEndPoint;
        var addresses = await resolver.ResolveAsync(endPoint.Host, ct);
        if (addresses.Length == 0)
        {
            throw new System.Net.Sockets.SocketException((int)System.Net.Sockets.SocketError.HostNotFound);
        }

        if (!allowPrivateNetwork && (addresses.Any(SsrfGuard.IsBlocked) || endPoint.Port is not (80 or 443)))
        {
            throw new SsrfBlockedException(endPoint.Host);
        }

        var socket = new System.Net.Sockets.Socket(System.Net.Sockets.SocketType.Stream, System.Net.Sockets.ProtocolType.Tcp) { NoDelay = true };
        try
        {
            await socket.ConnectAsync(addresses, endPoint.Port, ct);
            return new System.Net.Sockets.NetworkStream(socket, ownsSocket: true);
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }
}
