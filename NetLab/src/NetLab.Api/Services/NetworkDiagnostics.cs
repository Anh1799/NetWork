using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Options;
using NetLab.Api.Configuration;
using NetLab.Api.Models;

namespace NetLab.Api.Services;

public interface INetworkDiagnostics
{
    Task<DnsResult> ResolveDnsAsync(string host, CancellationToken ct = default);
    Task<PingResult> PingAsync(string host, int count, int timeoutMs, CancellationToken ct = default);
    Task<TcpCheckResult> CheckTcpAsync(string host, int port, int timeoutMs, CancellationToken ct = default);
    Task<HttpCheckResult> CheckHttpAsync(string url, int timeoutMs, CancellationToken ct = default);
    Task<TlsInfo> GetTlsInfoAsync(string host, int port, int timeoutMs, CancellationToken ct = default);
}

/// <summary>Các thao tác mạng cơ bản: DNS, ICMP ping, TCP connect, HTTP probe, TLS handshake.</summary>
public class NetworkDiagnostics(IHostGuard guard, IHttpClientFactory httpFactory, IOptions<NetworkOptions> options)
    : INetworkDiagnostics
{
    private readonly NetworkOptions _options = options.Value;

    private int ClampTimeout(int ms) => Math.Clamp(ms, 100, _options.MaxTimeoutMs);

    // ---------------------------------------------------------------- DNS
    public async Task<DnsResult> ResolveDnsAsync(string host, CancellationToken ct = default)
    {
        host = guard.NormalizeHost(host);

        var sw = Stopwatch.StartNew();
        var addresses = await guard.ResolveAsync(host, ct);
        sw.Stop();

        return new DnsResult(host, addresses.Select(a => a.ToString()).Distinct().ToList(), sw.Elapsed.TotalMilliseconds);
    }

    // ---------------------------------------------------------------- ICMP
    public async Task<PingResult> PingAsync(string host, int count, int timeoutMs, CancellationToken ct = default)
    {
        host = guard.NormalizeHost(host);
        count = Math.Clamp(count, 1, _options.MaxPingCount);
        timeoutMs = ClampTimeout(timeoutMs);

        var target = (await guard.ResolveAllowedAsync(host, ct))[0];
        var entries = new List<PingEntry>();

        try
        {
            using var ping = new Ping();
            for (var i = 1; i <= count; i++)
            {
                ct.ThrowIfCancellationRequested();

                var reply = await ping.SendPingAsync(target, timeoutMs);
                var success = reply.Status == IPStatus.Success;

                entries.Add(new PingEntry(
                    i,
                    reply.Status.ToString(),
                    success ? reply.RoundtripTime : null,
                    success ? reply.Options?.Ttl : null));

                if (i < count) await Task.Delay(200, ct);
            }
        }
        catch (PingException ex)
        {
            // Trên Linux/Docker ICMP có thể cần quyền đặc biệt
            throw new AppException(StatusCodes.Status503ServiceUnavailable,
                $"ICMP ping is not available in this environment: {ex.InnerException?.Message ?? ex.Message}");
        }

        var rtts = entries.Where(e => e.RoundtripMs.HasValue).Select(e => e.RoundtripMs!.Value).ToList();

        return new PingResult(
            host, target.ToString(), count, rtts.Count,
            Math.Round(100.0 * (count - rtts.Count) / count, 1),
            rtts.Count > 0 ? rtts.Min() : null,
            rtts.Count > 0 ? Math.Round(rtts.Average(), 1) : null,
            rtts.Count > 0 ? rtts.Max() : null,
            entries);
    }

    // ---------------------------------------------------------------- TCP
    public async Task<TcpCheckResult> CheckTcpAsync(string host, int port, int timeoutMs, CancellationToken ct = default)
    {
        host = guard.NormalizeHost(host);
        timeoutMs = ClampTimeout(timeoutMs);

        var address = (await guard.ResolveAllowedAsync(host, ct))[0];

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(timeoutMs);

        var sw = Stopwatch.StartNew();
        try
        {
            using var client = new TcpClient(address.AddressFamily);
            await client.ConnectAsync(address, port, cts.Token);
            sw.Stop();
            return new TcpCheckResult(host, address.ToString(), port, "Open", sw.Elapsed.TotalMilliseconds, null);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return new TcpCheckResult(host, address.ToString(), port, "Timeout", sw.Elapsed.TotalMilliseconds,
                $"No response within {timeoutMs} ms.");
        }
        catch (SocketException ex)
        {
            return new TcpCheckResult(host, address.ToString(), port, "Closed", sw.Elapsed.TotalMilliseconds,
                ex.SocketErrorCode.ToString());
        }
    }

    // ---------------------------------------------------------------- HTTP
    public async Task<HttpCheckResult> CheckHttpAsync(string url, int timeoutMs, CancellationToken ct = default)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
            throw new AppException(StatusCodes.Status400BadRequest, "url must be an absolute http or https URL.");

        timeoutMs = ClampTimeout(timeoutMs);

        var host = guard.NormalizeHost(uri.IdnHost);
        await guard.ResolveAllowedAsync(host, ct); // chặn sớm; ConnectCallback kiểm tra lại lúc kết nối

        var client = httpFactory.CreateClient("probe");

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(timeoutMs);

        var sw = Stopwatch.StartNew();

        HttpCheckResult Failure(string error) =>
            new(uri.ToString(), null, null, null, sw.Elapsed.TotalMilliseconds, null, null, null, null, error);

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token);
            sw.Stop();

            var server = response.Headers.Server.ToString();

            return new HttpCheckResult(
                uri.ToString(),
                (int)response.StatusCode,
                response.ReasonPhrase,
                response.Version.ToString(),
                sw.Elapsed.TotalMilliseconds,
                string.IsNullOrEmpty(server) ? null : server,
                response.Content.Headers.ContentType?.ToString(),
                response.Content.Headers.ContentLength,
                response.Headers.Location?.ToString(),
                null);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return Failure($"Timeout after {timeoutMs} ms.");
        }
        catch (HttpRequestException ex)
        {
            return Failure(ex.InnerException?.Message ?? ex.Message);
        }
    }

    // ---------------------------------------------------------------- TLS
    private sealed record CertSnapshot(string Subject, string Issuer, DateTime NotBefore, DateTime NotAfter, string? San);

    public async Task<TlsInfo> GetTlsInfoAsync(string host, int port, int timeoutMs, CancellationToken ct = default)
    {
        host = guard.NormalizeHost(host);
        timeoutMs = ClampTimeout(timeoutMs);

        var address = (await guard.ResolveAllowedAsync(host, ct))[0];

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(timeoutMs);

        var captured = new List<(CertSnapshot Cert, SslPolicyErrors Errors)>();
        var protocol = string.Empty;

        try
        {
            using var tcp = new TcpClient(address.AddressFamily);
            await tcp.ConnectAsync(address, port, cts.Token);

            // Chấp nhận mọi chứng chỉ để có thể ĐỌC thông tin; lỗi chứng chỉ được báo trong PolicyErrors.
            await using var ssl = new SslStream(tcp.GetStream(), false, (_, cert, _, errors) =>
            {
                if (cert is not null)
                {
                    var x509 = cert as X509Certificate2 ?? new X509Certificate2(cert);
                    var san = x509.Extensions.FirstOrDefault(e => e.Oid?.Value == "2.5.29.17")?.Format(false);
                    captured.Add((new CertSnapshot(
                        x509.Subject, x509.Issuer,
                        x509.NotBefore.ToUniversalTime(), x509.NotAfter.ToUniversalTime(), san), errors));
                }
                return true;
            });

            await ssl.AuthenticateAsClientAsync(new SslClientAuthenticationOptions { TargetHost = host }, cts.Token);
            protocol = ssl.SslProtocol.ToString();
        }
        catch (AuthenticationException ex)
        {
            throw new AppException(StatusCodes.Status502BadGateway,
                $"TLS handshake failed: {ex.InnerException?.Message ?? ex.Message}");
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new AppException(StatusCodes.Status504GatewayTimeout, "Connection timed out.");
        }
        catch (Exception ex) when (ex is SocketException or IOException)
        {
            throw new AppException(StatusCodes.Status502BadGateway, $"Connection failed: {ex.Message}");
        }

        var first = captured.FirstOrDefault();
        if (first.Cert is null)
            throw new AppException(StatusCodes.Status502BadGateway, "Server did not present a certificate.");

        var c = first.Cert;
        return new TlsInfo(
            host, port, protocol, c.Subject, c.Issuer, c.NotBefore, c.NotAfter,
            (int)Math.Floor((c.NotAfter - DateTime.UtcNow).TotalDays),
            c.San, first.Errors.ToString());
    }
}
