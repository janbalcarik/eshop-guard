using System.Net;
using System.Net.Sockets;

namespace EshopGuard.Core.Crawl;

/// <summary>
/// Addresses the crawler never connects to (protection against SSRF): internal, local, link-local (cloud metadata
/// 169.254.169.254), shared, documentation, benchmarking, multicast and reserved ranges of IPv4 and IPv6. An IPv4 address
/// mapped into IPv6 (<c>::ffff:10.0.0.1</c>) or carried in 6to4 (<c>2002::/16</c>) is checked as IPv4.
/// </summary>
internal static class SsrfGuard
{
    /// <summary>Error of a <see cref="FetchResponse"/> that was refused without a connection.</summary>
    public const string Error = "ssrf_blocked";

    private static readonly Network[] V4 =
    [
        Network.Parse("0.0.0.0/8"),
        Network.Parse("10.0.0.0/8"),
        Network.Parse("100.64.0.0/10"),
        Network.Parse("127.0.0.0/8"),
        Network.Parse("169.254.0.0/16"),
        Network.Parse("172.16.0.0/12"),
        Network.Parse("192.0.0.0/24"),
        Network.Parse("192.0.2.0/24"),
        Network.Parse("192.168.0.0/16"),
        Network.Parse("198.18.0.0/15"),
        Network.Parse("198.51.100.0/24"),
        Network.Parse("203.0.113.0/24"),
        Network.Parse("224.0.0.0/4"),
        Network.Parse("240.0.0.0/4"),
    ];

    private static readonly Network[] V6 =
    [
        Network.Parse("::/96"),
        Network.Parse("64:ff9b::/96"),
        Network.Parse("100::/64"),
        Network.Parse("2001:db8::/32"),
        Network.Parse("fc00::/7"),
        Network.Parse("fe80::/10"),
        Network.Parse("fec0::/10"),
        Network.Parse("ff00::/8"),
    ];

    private static readonly Network SixToFour = Network.Parse("2002::/16");

    /// <summary>True when the crawler must not connect to the address.</summary>
    public static bool IsBlocked(IPAddress address)
    {
        ArgumentNullException.ThrowIfNull(address);
        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            return V4.Any(n => n.Contains(address));
        }

        if (address.AddressFamily != AddressFamily.InterNetworkV6)
        {
            return true;
        }

        if (SixToFour.Contains(address))
        {
            var bytes = address.GetAddressBytes();
            return IsBlocked(new IPAddress(bytes[2..6]));
        }

        return V6.Any(n => n.Contains(address));
    }

    /// <summary>
    /// The URL can be requested at all: http or https, the default port and no user name or password in the address.
    /// The address it leads to is checked when connecting.
    /// </summary>
    public static bool IsAllowedUrl(Uri url) =>
        url.IsAbsoluteUri
        && (url.Scheme == Uri.UriSchemeHttp && url.Port == 80 || url.Scheme == Uri.UriSchemeHttps && url.Port == 443)
        && string.IsNullOrEmpty(url.UserInfo);

    private readonly record struct Network(byte[] Prefix, int Bits)
    {
        public static Network Parse(string cidr)
        {
            var slash = cidr.IndexOf('/');
            return new Network(IPAddress.Parse(cidr[..slash]).GetAddressBytes(), int.Parse(cidr[(slash + 1)..], System.Globalization.CultureInfo.InvariantCulture));
        }

        public bool Contains(IPAddress address)
        {
            var bytes = address.GetAddressBytes();
            if (bytes.Length != Prefix.Length)
            {
                return false;
            }

            var full = Bits / 8;
            for (var i = 0; i < full; i++)
            {
                if (bytes[i] != Prefix[i])
                {
                    return false;
                }
            }

            var rest = Bits % 8;
            if (rest == 0)
            {
                return true;
            }

            var mask = (byte)(0xFF << (8 - rest));
            return (bytes[full] & mask) == (Prefix[full] & mask);
        }
    }
}

/// <summary>The address of a host leads into an internal or local network; no connection was made.</summary>
public sealed class SsrfBlockedException(string target)
    : Exception($"{SsrfGuard.Error}: adresa {target} vede do vnitřní nebo místní sítě, nebo používá jiný port než 80 a 443; nic se nestahovalo. "
        + "Místní testovací e-shop povolí volba --allow-private-network.")
{
    /// <summary>The blocked host or URL.</summary>
    public string Target { get; } = target;
}
