using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using NetLab.Api.Configuration;
using NetLab.Api.Services;

namespace NetLab.Api.Middleware;

/// <summary>Yêu cầu header X-API-Key cho mọi đường dẫn /api/* (nếu Security:ApiKey được cấu hình).</summary>
public class ApiKeyMiddleware(RequestDelegate next, IOptions<SecurityOptions> options)
{
    public const string HeaderName = "X-API-Key";

    public async Task InvokeAsync(HttpContext context)
    {
        var expected = options.Value.ApiKey;

        if (!string.IsNullOrEmpty(expected) && context.Request.Path.StartsWithSegments("/api"))
        {
            var provided = context.Request.Headers[HeaderName].ToString();

            // So sánh thời gian cố định để tránh timing attack
            var ok = CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(provided), Encoding.UTF8.GetBytes(expected));

            if (!ok) throw new AppException(StatusCodes.Status401Unauthorized, "Missing or invalid API key.");
        }

        await next(context);
    }
}
