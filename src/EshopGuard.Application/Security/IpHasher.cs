using System.Net;
using System.Security.Cryptography;
using System.Text;
using EshopGuard.Application.Options;
using Microsoft.Extensions.Options;

namespace EshopGuard.Application.Security;

/// <summary>
/// HMAC-SHA256 of e-mail addresses and IP addresses with <c>Security:IpHashKey</c> (user-secrets): the keys of the limits,
/// the audit and the logs carry only these hashes, never a readable address (AD 5, AD 13). A missing key stops the start.
/// </summary>
public sealed class IpHasher
{
    private readonly byte[] _key;

    public IpHasher(IOptions<SecurityOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _key = Convert.FromBase64String(options.Value.IpHashKey);
    }

    /// <summary>Hash of a normalized e-mail address (64 hexadecimal characters).</summary>
    public string HashEmail(string normalizedEmail) => Hash("email", normalizedEmail);

    /// <summary>Hash of an IP address (IPv4 mapped to IPv6 is unmapped first; a missing address hashes as <c>unknown</c>).</summary>
    public string HashIp(IPAddress? address) =>
        Hash("ip", address is null ? "unknown" : (address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address).ToString());

    /// <summary><see cref="HashIp"/> as 32 bytes for <c>user_tokens.requested_ip_hash</c>.</summary>
    public byte[] HashIpBytes(IPAddress? address) => Convert.FromHexString(HashIp(address));

    private string Hash(string kind, string value) =>
        Convert.ToHexStringLower(HMACSHA256.HashData(_key, Encoding.UTF8.GetBytes(kind + ":" + value)));
}
