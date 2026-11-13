using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using Microsoft.AspNetCore.Mvc;

namespace NetLab.Api.Controllers;

[ApiController]
[Route("api/info")]
[Produces("application/json")]
public class InfoController : ControllerBase
{
    private static readonly HashSet<string> SensitiveHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "Authorization", "Cookie", "X-API-Key"
    };

    /// <summary>Thông tin kết nối của request hiện tại (IP client, giao thức, header...).</summary>
    [HttpGet("request")]
    public IActionResult RequestInfo() => Ok(new
    {
        clientIp = HttpContext.Connection.RemoteIpAddress?.ToString(),
        clientPort = HttpContext.Connection.RemotePort,
        localIp = HttpContext.Connection.LocalIpAddress?.ToString(),
        localPort = HttpContext.Connection.LocalPort,
        scheme = Request.Scheme,
        protocol = Request.Protocol,
        host = Request.Host.Value,
        method = Request.Method,
        forwardedFor = Request.Headers["X-Forwarded-For"].ToString(),
        headers = Request.Headers
            .Where(h => !SensitiveHeaders.Contains(h.Key))
            .ToDictionary(h => h.Key, h => h.Value.ToString())
    });

    /// <summary>Thông tin server và các network interface đang hoạt động.</summary>
    [HttpGet("server")]
    public IActionResult ServerInfo() => Ok(new
    {
        machineName = Environment.MachineName,
        os = RuntimeInformation.OSDescription,
        framework = RuntimeInformation.FrameworkDescription,
        networkAvailable = NetworkInterface.GetIsNetworkAvailable(),
        interfaces = NetworkInterface.GetAllNetworkInterfaces()
            .Where(n => n.OperationalStatus == OperationalStatus.Up && n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
            .Select(n => new
            {
                name = n.Name,
                type = n.NetworkInterfaceType.ToString(),
                speedMbps = n.Speed > 0 ? n.Speed / 1_000_000 : (long?)null,
                addresses = n.GetIPProperties().UnicastAddresses.Select(a => a.Address.ToString())
            }),
        serverTimeUtc = DateTime.UtcNow
    });
}
