using System.Net;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using NetLab.Api.Clients;
using NetLab.Api.Services;

namespace NetLab.Tests;

/// <summary>Test tầng typed HttpClient mà không cần Internet.</summary>
public class HolidayClientTests
{
    private static HolidayClient Create(StubHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("https://example.test/api/v3/") },
            new MemoryCache(new MemoryCacheOptions()),
            NullLogger<HolidayClient>.Instance);

    [Fact]
    public async Task ParsesResponse_AndBuildsCorrectUrl()
    {
        const string json = """
            [
              { "date": "2026-01-01", "localName": "Tết Dương lịch", "name": "New Year's Day", "countryCode": "VN" },
              { "date": "2026-09-02", "localName": "Quốc khánh", "name": "National Day", "countryCode": "VN" }
            ]
            """;
        var handler = new StubHandler(HttpStatusCode.OK, json);

        var result = await Create(handler).GetPublicHolidaysAsync(2026, "vn");

        Assert.Equal(2, result.Count);
        Assert.Equal(new DateOnly(2026, 1, 1), result[0].Date);
        Assert.Equal("https://example.test/api/v3/PublicHolidays/2026/VN", handler.LastUri!.ToString());
    }

    [Fact]
    public async Task SecondCall_IsServedFromCache()
    {
        var handler = new StubHandler(HttpStatusCode.OK, "[]");
        var client = Create(handler);

        await client.GetPublicHolidaysAsync(2026, "VN");
        await client.GetPublicHolidaysAsync(2026, "VN");

        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task NotFound_Maps_To404()
    {
        var client = Create(new StubHandler(HttpStatusCode.NotFound, "{}"));

        var ex = await Assert.ThrowsAsync<AppException>(() => client.GetPublicHolidaysAsync(2026, "ZZ"));

        Assert.Equal(404, ex.StatusCode);
    }

    [Fact]
    public async Task ServerError_Maps_To502()
    {
        var client = Create(new StubHandler(HttpStatusCode.InternalServerError, "{}"));

        var ex = await Assert.ThrowsAsync<AppException>(() => client.GetPublicHolidaysAsync(2026, "VN"));

        Assert.Equal(502, ex.StatusCode);
    }
}
