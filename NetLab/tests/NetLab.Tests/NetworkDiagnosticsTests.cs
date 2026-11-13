using System.Net;
using System.Net.Sockets;
using NetLab.Api.Services;

namespace NetLab.Tests;

public class NetworkDiagnosticsTests
{
    [Fact]
    public async Task Tcp_OpenPort_ReportsOpen()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;

        var result = await TestHelpers.Diagnostics(allowPrivate: true).CheckTcpAsync("127.0.0.1", port, 2000);

        Assert.Equal("Open", result.Status);
        Assert.Equal(port, result.Port);
    }

    [Fact]
    public async Task Tcp_ClosedPort_ReportsClosed()
    {
        // Mở rồi đóng listener để chắc chắn cổng này không còn ai lắng nghe
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();

        var result = await TestHelpers.Diagnostics(allowPrivate: true).CheckTcpAsync("127.0.0.1", port, 2000);

        Assert.Equal("Closed", result.Status);
    }

    [Fact]
    public async Task Tcp_Loopback_IsBlockedByDefault()
    {
        var ex = await Assert.ThrowsAsync<AppException>(() =>
            TestHelpers.Diagnostics().CheckTcpAsync("127.0.0.1", 80, 1000));

        Assert.Equal(403, ex.StatusCode);
    }

    [Fact]
    public async Task Dns_Localhost_ReturnsAddresses()
    {
        var result = await TestHelpers.Diagnostics().ResolveDnsAsync("localhost");

        Assert.NotEmpty(result.Addresses);
    }

    [Fact]
    public async Task Http_ParsesStatusAndHeaders_AndDoesNotFollowRedirect()
    {
        var handler = new StubHandler(HttpStatusCode.MovedPermanently, "", r =>
        {
            r.Headers.Location = new Uri("https://example.test/new");
            r.Headers.Server.ParseAdd("StubServer/1.0");
        });

        // IP literal công khai: không cần DNS thật, và không bị HostGuard chặn
        var result = await TestHelpers.Diagnostics(handler: handler).CheckHttpAsync("http://93.184.216.34/old", 2000);

        Assert.Equal(301, result.StatusCode);
        Assert.Equal("https://example.test/new", result.RedirectLocation);
        Assert.Equal("StubServer/1.0", result.Server);
        Assert.Null(result.Error);
    }

    [Fact]
    public async Task Http_InternalTarget_IsForbidden()
    {
        var ex = await Assert.ThrowsAsync<AppException>(() =>
            TestHelpers.Diagnostics().CheckHttpAsync("http://169.254.169.254/latest/meta-data/", 2000));

        Assert.Equal(403, ex.StatusCode);
    }

    [Theory]
    [InlineData("ftp://example.com")]
    [InlineData("not a url")]
    [InlineData("file:///etc/passwd")]
    public async Task Http_InvalidScheme_ReturnsBadRequest(string url)
    {
        var ex = await Assert.ThrowsAsync<AppException>(() => TestHelpers.Diagnostics().CheckHttpAsync(url, 2000));

        Assert.Equal(400, ex.StatusCode);
    }
}
