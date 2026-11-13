using System.Net;
using System.Text;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using TaskFlow.Api.Services;

namespace TaskFlow.Tests;

/// <summary>Test tầng networking mà không cần Internet: thay HttpMessageHandler bằng stub.</summary>
public class HolidayClientTests
{
    private sealed class StubHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public int CallCount { get; private set; }
        public Uri? LastUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            CallCount++;
            LastUri = request.RequestUri;
            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });
        }
    }

    private static HolidayClient CreateClient(StubHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("https://example.test/api/v3/") },
            new MemoryCache(new MemoryCacheOptions()),
            NullLogger<HolidayClient>.Instance);

    [Fact]
    public async Task GetPublicHolidays_ParsesResponse_AndBuildsCorrectUrl()
    {
        const string json = """
            [
              { "date": "2026-01-01", "localName": "Tết Dương lịch", "name": "New Year's Day", "countryCode": "VN" },
              { "date": "2026-09-02", "localName": "Quốc khánh", "name": "National Day", "countryCode": "VN" }
            ]
            """;
        var handler = new StubHandler(HttpStatusCode.OK, json);

        var result = await CreateClient(handler).GetPublicHolidaysAsync(2026, "vn");

        Assert.Equal(2, result.Count);
        Assert.Equal(new DateOnly(2026, 1, 1), result[0].Date);
        Assert.Equal("New Year's Day", result[0].Name);
        Assert.Equal("https://example.test/api/v3/PublicHolidays/2026/VN", handler.LastUri!.ToString());
    }

    [Fact]
    public async Task GetPublicHolidays_SecondCall_IsServedFromCache()
    {
        var handler = new StubHandler(HttpStatusCode.OK, "[]");
        var client = CreateClient(handler);

        await client.GetPublicHolidaysAsync(2026, "VN");
        await client.GetPublicHolidaysAsync(2026, "VN");

        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task GetPublicHolidays_NotFound_ThrowsAppException404()
    {
        var client = CreateClient(new StubHandler(HttpStatusCode.NotFound, "{}"));

        var ex = await Assert.ThrowsAsync<AppException>(() => client.GetPublicHolidaysAsync(2026, "ZZ"));

        Assert.Equal(404, ex.StatusCode);
    }

    [Fact]
    public async Task GetPublicHolidays_ServerError_ThrowsAppException502()
    {
        var client = CreateClient(new StubHandler(HttpStatusCode.InternalServerError, "{}"));

        var ex = await Assert.ThrowsAsync<AppException>(() => client.GetPublicHolidaysAsync(2026, "VN"));

        Assert.Equal(502, ex.StatusCode);
    }
}
