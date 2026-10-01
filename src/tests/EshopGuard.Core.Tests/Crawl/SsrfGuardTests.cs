using System.Net;
using EshopGuard.Core.Crawl;

namespace EshopGuard.Core.Tests;

/// <summary>Addresses the crawler never connects to (task 6.1).</summary>
public sealed class SsrfGuardTests
{
    [Theory]
    [InlineData("0.0.0.0")]
    [InlineData("0.255.255.255")]
    [InlineData("10.0.0.0")]
    [InlineData("10.255.255.255")]
    [InlineData("100.64.0.1")]
    [InlineData("100.127.255.255")]
    [InlineData("127.0.0.1")]
    [InlineData("127.255.255.254")]
    [InlineData("169.254.169.254")]
    [InlineData("172.16.0.0")]
    [InlineData("172.31.255.255")]
    [InlineData("192.0.0.1")]
    [InlineData("192.0.2.10")]
    [InlineData("192.168.0.1")]
    [InlineData("192.168.255.255")]
    [InlineData("198.18.0.1")]
    [InlineData("198.19.255.255")]
    [InlineData("198.51.100.7")]
    [InlineData("203.0.113.9")]
    [InlineData("224.0.0.1")]
    [InlineData("239.255.255.255")]
    [InlineData("240.0.0.1")]
    [InlineData("255.255.255.255")]
    [InlineData("::")]
    [InlineData("::1")]
    [InlineData("::ffff:10.0.0.1")]
    [InlineData("::ffff:127.0.0.1")]
    [InlineData("::ffff:169.254.169.254")]
    [InlineData("64:ff9b::a00:1")]
    [InlineData("2001:db8::1")]
    [InlineData("fc00::1")]
    [InlineData("fdff:ffff::1")]
    [InlineData("fe80::1")]
    [InlineData("febf::1")]
    [InlineData("ff02::1")]
    [InlineData("2002:a00:1::1")]
    public void InternalAndReservedAddresses_AreBlocked(string address) =>
        Assert.True(SsrfGuard.IsBlocked(IPAddress.Parse(address)));

    [Theory]
    [InlineData("1.1.1.1")]
    [InlineData("9.255.255.255")]
    [InlineData("11.0.0.0")]
    [InlineData("100.63.255.255")]
    [InlineData("100.128.0.0")]
    [InlineData("169.253.255.255")]
    [InlineData("172.15.255.255")]
    [InlineData("172.32.0.0")]
    [InlineData("192.167.255.255")]
    [InlineData("192.169.0.0")]
    [InlineData("93.184.216.34")]
    [InlineData("223.255.255.255")]
    [InlineData("::ffff:93.184.216.34")]
    [InlineData("2a00:1450:4014:80c::200e")]
    [InlineData("2002:5db8:d822::1")]
    public void PublicAddresses_AreAllowed(string address) =>
        Assert.False(SsrfGuard.IsBlocked(IPAddress.Parse(address)));

    [Theory]
    [InlineData("https://shop.example/", true)]
    [InlineData("http://shop.example/a?b=c", true)]
    [InlineData("https://shop.example:443/", true)]
    [InlineData("https://shop.example:8443/", false)]
    [InlineData("http://shop.example:8000/", false)]
    [InlineData("https://user:heslo@shop.example/", false)]
    [InlineData("ftp://shop.example/", false)]
    [InlineData("file:///etc/passwd", false)]
    public void Urls_OnlyHttpAndHttpsOnDefaultPortsWithoutCredentials(string url, bool allowed) =>
        Assert.Equal(allowed, SsrfGuard.IsAllowedUrl(new Uri(url)));
}
