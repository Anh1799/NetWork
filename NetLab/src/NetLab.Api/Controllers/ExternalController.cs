using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using NetLab.Api.Clients;
using NetLab.Api.Models;

namespace NetLab.Api.Controllers;

/// <summary>Ví dụ gọi Web API bên ngoài qua typed HttpClient (retry, timeout, circuit breaker).</summary>
[ApiController]
[Route("api/external")]
[Produces("application/json")]
public partial class ExternalController(IHolidayClient holidays, IPublicIpClient publicIp) : ControllerBase
{
    [GeneratedRegex("^[A-Za-z]{2}$")]
    private static partial Regex CountryCode();

    /// <summary>Ngày lễ quốc gia, ví dụ /api/external/holidays/2026/VN.</summary>
    [HttpGet("holidays/{year:int:range(1970,2100)}/{countryCode}")]
    public async Task<IActionResult> Holidays(int year, string countryCode, CancellationToken ct)
    {
        if (!CountryCode().IsMatch(countryCode))
            return Problem(statusCode: StatusCodes.Status400BadRequest,
                detail: "countryCode must be a 2-letter ISO 3166-1 code, e.g. VN, US, JP.");

        return Ok(await holidays.GetPublicHolidaysAsync(year, countryCode, ct));
    }

    /// <summary>IP public của chính server này (hỏi dịch vụ ipify).</summary>
    [HttpGet("public-ip")]
    public async Task<IActionResult> PublicIp(CancellationToken ct)
        => Ok(new { ip = await publicIp.GetPublicIpAsync(ct) });
}
