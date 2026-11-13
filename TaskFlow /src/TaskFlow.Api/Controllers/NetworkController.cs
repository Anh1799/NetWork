using Microsoft.AspNetCore.Mvc;

namespace TaskFlow.Api.Controllers;

/// <summary>Endpoint công khai hiển thị thông tin kết nối mạng của request hiện tại.</summary>
[ApiController]
[Route("api/network")]
[Produces("application/json")]
public class NetworkController : ControllerBase
{
    [HttpGet("info")]
    public IActionResult Info() => Ok(new
    {
        clientIp = HttpContext.Connection.RemoteIpAddress?.ToString(),
        scheme = Request.Scheme,
        protocol = Request.Protocol,
        host = Request.Host.Value,
        userAgent = Request.Headers.UserAgent.ToString(),
        serverName = Environment.MachineName,
        serverTimeUtc = DateTime.UtcNow
    });
}
