using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using NetLab.Api.Models;
using NetLab.Api.Services;

namespace NetLab.Api.Clients;

public interface IHolidayClient
{
    Task<IReadOnlyList<HolidayDto>> GetPublicHolidaysAsync(int year, string countryCode, CancellationToken ct = default);
}

/// <summary>Typed HttpClient gọi https://date.nager.at, có cache và ánh xạ lỗi mạng.</summary>
public class HolidayClient(HttpClient http, IMemoryCache cache, ILogger<HolidayClient> logger) : IHolidayClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    internal sealed record NagerHoliday(DateOnly Date, string LocalName, string Name);

    public async Task<IReadOnlyList<HolidayDto>> GetPublicHolidaysAsync(int year, string countryCode, CancellationToken ct = default)
    {
        var code = countryCode.ToUpperInvariant();
        var key = $"holidays:{code}:{year}";

        if (cache.TryGetValue(key, out IReadOnlyList<HolidayDto>? cached) && cached is not null)
            return cached;

        try
        {
            using var response = await http.GetAsync($"PublicHolidays/{year}/{code}", ct);

            if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.BadRequest)
                throw new AppException(StatusCodes.Status404NotFound, $"No holiday data for '{code}' in {year}.");

            response.EnsureSuccessStatusCode();

            var data = await response.Content.ReadFromJsonAsync<List<NagerHoliday>>(JsonOptions, ct) ?? new();
            IReadOnlyList<HolidayDto> result = data.Select(h => new HolidayDto(h.Date, h.Name, h.LocalName)).ToList();

            cache.Set(key, result, TimeSpan.FromHours(12));
            return result;
        }
        catch (Exception ex) when (HttpErrorMapper.TryMap(ex, "holiday", logger, out var mapped))
        {
            throw mapped!;
        }
    }
}
