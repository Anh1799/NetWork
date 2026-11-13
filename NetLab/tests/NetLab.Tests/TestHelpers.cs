using System.Net;
using System.Text;
using Microsoft.Extensions.Options;
using NetLab.Api.Configuration;
using NetLab.Api.Services;

namespace NetLab.Tests;

internal static class TestHelpers
{
    public static IOptions<NetworkOptions> Opts(bool allowPrivate = false) =>
        Microsoft.Extensions.Options.Options.Create(new NetworkOptions { AllowPrivateAddresses = allowPrivate });

    public static HostGuard Guard(bool allowPrivate = false) => new(Opts(allowPrivate));

    public static NetworkDiagnostics Diagnostics(bool allowPrivate = false, HttpMessageHandler? handler = null) =>
        new(Guard(allowPrivate), new StubHttpClientFactory(handler ?? new StubHandler(HttpStatusCode.OK)), Opts(allowPrivate));
}

/// <summary>HttpMessageHandler giả: trả về response định sẵn, không cần Internet.</summary>
internal sealed class StubHandler(HttpStatusCode status, string body = "", Action<HttpResponseMessage>? customize = null)
    : HttpMessageHandler
{
    public int CallCount { get; private set; }
    public Uri? LastUri { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        CallCount++;
        LastUri = request.RequestUri;

        var response = new HttpResponseMessage(status)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        customize?.Invoke(response);
        return Task.FromResult(response);
    }
}

internal sealed class StubHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
{
    public HttpClient CreateClient(string name) =>
        new(handler, disposeHandler: false) { Timeout = Timeout.InfiniteTimeSpan };
}
