using System.Net;
using NetLab.Api.Services;

namespace NetLab.Tests;

public class HostGuardTests
{
    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("10.1.2.3")]
    [InlineData("172.16.0.1")]
    [InlineData("172.31.255.255")]
    [InlineData("192.168.1.10")]
    [InlineData("169.254.169.254")]   // cloud metadata
    [InlineData("100.64.0.1")]
    [InlineData("0.0.0.0")]
    [InlineData("224.0.0.1")]
    [InlineData("::1")]
    [InlineData("fe80::1")]
    [InlineData("fd00::1")]
    [InlineData("::ffff:10.0.0.1")]   // IPv4-mapped IPv6
    public void BlocksInternalAddresses(string ip)
    {
        Assert.False(TestHelpers.Guard().IsAddressAllowed(IPAddress.Parse(ip)));
    }

    [Theory]
    [InlineData("8.8.8.8")]
    [InlineData("1.1.1.1")]
    [InlineData("93.184.216.34")]
    [InlineData("172.32.0.1")]        // ngay ngoài dải 172.16/12
    [InlineData("2606:4700:4700::1111")]
    public void AllowsPublicAddresses(string ip)
    {
        Assert.True(TestHelpers.Guard().IsAddressAllowed(IPAddress.Parse(ip)));
    }

    [Fact]
    public void AllowsEverything_WhenPrivateAddressesEnabled()
    {
        Assert.True(TestHelpers.Guard(allowPrivate: true).IsAddressAllowed(IPAddress.Loopback));
    }

    [Theory]
    [InlineData("")]
    [InlineData("a b")]
    [InlineData("evil.com/path")]
    [InlineData("host;rm -rf")]
    public void NormalizeHost_RejectsInvalidInput(string host)
    {
        var ex = Assert.Throws<AppException>(() => TestHelpers.Guard().NormalizeHost(host));
        Assert.Equal(400, ex.StatusCode);
    }

    [Fact]
    public async Task ResolveAllowed_ForLoopbackIp_ThrowsForbidden()
    {
        var ex = await Assert.ThrowsAsync<AppException>(() =>
            TestHelpers.Guard().ResolveAllowedAsync("127.0.0.1", CancellationToken.None));

        Assert.Equal(403, ex.StatusCode);
    }
}
