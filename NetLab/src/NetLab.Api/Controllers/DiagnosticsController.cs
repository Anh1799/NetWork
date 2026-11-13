using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using NetLab.Api.Models;
using NetLab.Api.Services;

namespace NetLab.Api.Controllers;

/// <summary>Công cụ chẩn đoán mạng. Địa chỉ nội bộ bị chặn (xem HostGuard).</summary>
[ApiController]
[Route("api/diagnostics")]
[Produces("application/json")]
public class DiagnosticsController(INetworkDiagnostics diagnostics) : ControllerBase
{
    /// <summary>Phân giải DNS của một tên miền.</summary>
    [HttpGet("dns/{host}")]
    public async Task<ActionResult<DnsResult>> Dns(string host, CancellationToken ct)
        => Ok(await diagnostics.ResolveDnsAsync(host, ct));

    /// <summary>Gửi gói ICMP echo (ping) tới host.</summary>
    [HttpGet("ping/{host}")]
    public async Task<ActionResult<PingResult>> Ping(
        string host,
        [FromQuery, Range(1, 20)] int count = 4,
        [FromQuery, Range(100, 10000)] int timeoutMs = 2000,
        CancellationToken ct = default)
        => Ok(await diagnostics.PingAsync(host, count, timeoutMs, ct));

    /// <summary>Thử mở kết nối TCP tới host:port.</summary>
    [HttpGet("tcp")]
    public async Task<ActionResult<TcpCheckResult>> Tcp(
        [FromQuery, Required] string host,
        [FromQuery, Range(1, 65535)] int port,
        [FromQuery, Range(100, 10000)] int timeoutMs = 3000,
        CancellationToken ct = default)
        => Ok(await diagnostics.CheckTcpAsync(host, port, timeoutMs, ct));

    /// <summary>Gửi HTTP GET (không theo redirect) và trả về status, header, độ trễ.</summary>
    [HttpGet("http")]
    public async Task<ActionResult<HttpCheckResult>> Http(
        [FromQuery, Required] string url,
        [FromQuery, Range(100, 10000)] int timeoutMs = 5000,
        CancellationToken ct = default)
        => Ok(await diagnostics.CheckHttpAsync(url, timeoutMs, ct));

    /// <summary>Đọc thông tin chứng chỉ TLS của server (hạn dùng, issuer, SAN...).</summary>
    [HttpGet("tls")]
    public async Task<ActionResult<TlsInfo>> Tls(
        [FromQuery, Required] string host,
        [FromQuery, Range(1, 65535)] int port = 443,
        [FromQuery, Range(100, 10000)] int timeoutMs = 5000,
        CancellationToken ct = default)
        => Ok(await diagnostics.GetTlsInfoAsync(host, port, timeoutMs, ct));
}
