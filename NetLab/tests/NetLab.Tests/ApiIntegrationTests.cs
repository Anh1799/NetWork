using System.Net;
using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace NetLab.Tests;

public class ApiIntegrationTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private static CancellationToken Token() => new CancellationTokenSource(TimeSpan.FromSeconds(10)).Token;

    private Uri WsUri(string path) =>
        new UriBuilder(factory.Server.BaseAddress) { Scheme = "ws", Path = path }.Uri;

    private static async Task<string> ReceiveTextAsync(WebSocket ws, CancellationToken ct)
    {
        var buffer = new byte[4096];
        var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), ct);
        return Encoding.UTF8.GetString(buffer, 0, result.Count);
    }

    private static Task SendTextAsync(WebSocket ws, string text, CancellationToken ct) =>
        ws.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(text)), WebSocketMessageType.Text, true, ct);

    [Fact]
    public async Task Health_ReturnsOk()
    {
        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync("health")).StatusCode);
    }

    [Fact]
    public async Task Response_ContainsCorrelationId_AndEchoesClientValue()
    {
        var generated = await _client.GetAsync("health");
        Assert.True(generated.Headers.Contains("X-Correlation-ID"));

        var request = new HttpRequestMessage(HttpMethod.Get, "health");
        request.Headers.Add("X-Correlation-ID", "my-trace-123");
        var echoed = await _client.SendAsync(request);

        Assert.Equal("my-trace-123", echoed.Headers.GetValues("X-Correlation-ID").Single());
    }

    [Fact]
    public async Task RequestInfo_HidesSensitiveHeaders()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "api/info/request");
        request.Headers.Add("Authorization", "Bearer secret");
        request.Headers.Add("X-Custom", "hello");

        var response = await _client.SendAsync(request);
        var json = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("hello", json);
        Assert.DoesNotContain("Bearer secret", json);
    }

    [Fact]
    public async Task Dns_Localhost_Works()
    {
        var response = await _client.GetAsync("api/diagnostics/dns/localhost");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Tcp_Loopback_IsForbidden()
    {
        var response = await _client.GetAsync("api/diagnostics/tcp?host=127.0.0.1&port=80");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Theory]
    [InlineData("api/diagnostics/tcp?host=example.com&port=0")]
    [InlineData("api/diagnostics/tcp?host=example.com&port=70000")]
    [InlineData("api/diagnostics/tcp?port=80")]
    [InlineData("api/diagnostics/ping/example.com?count=999")]
    public async Task InvalidQuery_Returns400(string url)
    {
        Assert.Equal(HttpStatusCode.BadRequest, (await _client.GetAsync(url)).StatusCode);
    }

    [Fact]
    public async Task Holidays_InvalidCountryCode_Returns400()
    {
        Assert.Equal(HttpStatusCode.BadRequest, (await _client.GetAsync("api/external/holidays/2026/VNM")).StatusCode);
    }

    [Fact]
    public async Task Sse_StreamsRequestedNumberOfEvents()
    {
        var response = await _client.GetAsync("api/stream/time?count=3&intervalMs=10");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal("text/event-stream", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(3, body.Split("event: tick", StringSplitOptions.None).Length - 1);
    }

    [Fact]
    public async Task WebSocket_Echo_ReturnsSameMessage()
    {
        var ct = Token();
        using var ws = await factory.Server.CreateWebSocketClient().ConnectAsync(WsUri("ws/echo"), ct);

        await SendTextAsync(ws, "ping 123", ct);

        Assert.Equal("ping 123", await ReceiveTextAsync(ws, ct));
        await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "done", ct);
    }

    [Fact]
    public async Task WebSocket_Chat_BroadcastsToOtherClients()
    {
        var ct = Token();
        var wsClient = factory.Server.CreateWebSocketClient();

        using var alice = await wsClient.ConnectAsync(WsUri("ws/chat?name=alice"), ct);
        using var bob = await wsClient.ConnectAsync(WsUri("ws/chat?name=bob"), ct);

        await SendTextAsync(alice, "hello bob", ct);

        // Bob nhận cả thông báo join lẫn tin nhắn; đọc tối đa vài message để tìm tin nhắn của Alice
        var received = new List<string>();
        for (var i = 0; i < 6 && !received.Any(m => m == "alice: hello bob"); i++)
            received.Add(await ReceiveTextAsync(bob, ct));

        Assert.Contains("alice: hello bob", received);
    }
}

public class ApiKeyTests(SecuredApiFactory factory) : IClassFixture<SecuredApiFactory>
{
    [Fact]
    public async Task ApiEndpoints_WithoutKey_Return401()
    {
        var response = await factory.CreateClient().GetAsync("api/info/server");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ApiEndpoints_WithWrongKey_Return401()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-API-Key", "wrong");

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("api/info/server")).StatusCode);
    }

    [Fact]
    public async Task ApiEndpoints_WithCorrectKey_Return200()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-API-Key", SecuredApiFactory.Key);

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("api/info/server")).StatusCode);
    }

    [Fact]
    public async Task Health_DoesNotRequireKey()
    {
        Assert.Equal(HttpStatusCode.OK, (await factory.CreateClient().GetAsync("health")).StatusCode);
    }
}
