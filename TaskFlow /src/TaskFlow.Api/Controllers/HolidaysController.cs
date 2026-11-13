using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskFlow.Api.Dtos;
using TaskFlow.Api.Services;

namespace TaskFlow.Api.Controllers;

/// <summary>Ví dụ gọi một Web API bên ngoài thông qua typed HttpClient.</summary>
[ApiController]
[Authorize]
[Route("api/holidays")]
[Produces("application/json")]
public class HolidaysController(IHolidayClient holidays) : ControllerBase
{
    private static readonly Regex CountryCodeRegex = new("^[A-Za-z]{2}$", RegexOptions.Compiled);

    /// <summary>Ngày lễ quốc gia theo năm, ví dụ /api/holidays/2026/VN.</summary>
    [HttpGet("{year:int:range(1970,2100)}/{countryCode}")]
    [ProducesResponseType(typeof(IReadOnlyList<HolidayDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    [ProducesResponseType(StatusCodes.Status504GatewayTimeout)]
    public async Task<IActionResult> Get(int year, string countryCode, CancellationToken ct)
    {
        if (!CountryCodeRegex.IsMatch(countryCode))
            return Problem(statusCode: StatusCodes.Status400BadRequest,
                detail: "countryCode must be a 2-letter ISO 3166-1 code, e.g. VN, US, JP.");

        return Ok(await holidays.GetPublicHolidaysAsync(year, countryCode, ct));
    }
}
