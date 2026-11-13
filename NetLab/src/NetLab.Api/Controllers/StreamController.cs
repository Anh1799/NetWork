using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace NetLab.Api.Controllers;

[ApiController]
[Route("api/stream")]
public class StreamController : ControllerBase
{
    /// <summary>
    /// Server-Sent Events (text/event-stream): server đẩy dữ liệu một chiều qua một kết nối HTTP kéo dài.
    /// </summary>
    [HttpGet("time")]
    [Produces("text/event-stream")]
    public async Task Time(
        [FromQuery, Range(1, 1000)] int count = 10,
        [FromQuery, Range(10, 5000)] int intervalMs = 1000,
        CancellationToken ct = default)
    {
        Response.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers["X-Accel-Buffering"] = "no"; // tắt buffer của nginx

        try
        {
            for (var i = 1; i <= count; i++)
            {
                var payload = JsonSerializer.Serialize(new { seq = i, timeUtc = DateTime.UtcNow });
                await Response.WriteAsync($"id: {i}\nevent: tick\ndata: {payload}\n\n", ct);
                await Response.Body.FlushAsync(ct);

                if (i < count) await Task.Delay(intervalMs, ct);
            }
        }
        catch (OperationCanceledException)
        {
            // Client đóng kết nối - kết thúc êm
        }
    }
}
