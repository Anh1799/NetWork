using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Polly.CircuitBreaker;
using Polly.Timeout;
using TaskFlow.Api.Dtos;

namespace TaskFlow.Api.Services;

public interface IHolidayClient
{
    Task<IReadOnlyList<HolidayDto>> GetPublicHolidaysAsync(int year, string countryCode, CancellationToken ct = default);
}

/// <summary>
/// Typed HttpClient gọi API công khai https://date.nager.at.
/// Minh họa: BaseAddress, typed client, cache, resilience (retry/timeout/circuit breaker)
/// và ánh xạ lỗi mạng sang HTTP status phù hợp (502/504).
/// </summary>
public class HolidayClient(HttpClient http, IMemoryCache cache, ILogger<HolidayClient> logger) : IHolidayClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    // Cấu trúc JSON trả về từ Nager.Date (chỉ lấy các trường cần dùng)
    internal sealed record NagerHoliday(DateOnly Date, string LocalName, string Name);

    public async Task<IReadOnlyList<HolidayDto>> GetPublicHolidaysAsync(
        int year, string countryCode, CancellationToken ct = default)
    {
        var code = countryCode.ToUpperInvariant();
        var cacheKey = $"holidays:{code}:{year}";

        if (cache.TryGetValue(cacheKey, out IReadOnlyList<HolidayDto>? cached) && cached is not null)
            return cached;

        try
        {
            using var response = await http.GetAsync($"PublicHolidays/{year}/{code}", ct);

            if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.BadRequest)
                throw new AppException(StatusCodes.Status404NotFound,
                    $"No holiday data for country '{code}' and year {year}.");

            response.EnsureSuccessStatusCode();

            var data = await response.Content.ReadFromJsonAsync<List<NagerHoliday>>(JsonOptions, ct) ?? new();

            IReadOnlyList<HolidayDto> result = data
                .Select(h => new HolidayDto(h.Date, h.Name, h.LocalName))
                .ToList();

            cache.Set(cacheKey, result, TimeSpan.FromHours(12));
            return result;
        }
        catch (Exception ex) when (ex is TimeoutRejectedException)
        {
            logger.LogWarning(ex, "Holiday API timed out.");
            throw new AppException(StatusCodes.Status504GatewayTimeout, "The holiday service timed out.");
        }
        catch (Exception ex) when (ex is HttpRequestException or BrokenCircuitException)
        {
            logger.LogWarning(ex, "Holiday API is unavailable.");
            throw new AppException(StatusCodes.Status502BadGateway, "The holiday service is currently unavailable.");
        }
    }
}
