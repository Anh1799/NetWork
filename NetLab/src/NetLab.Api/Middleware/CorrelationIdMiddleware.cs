using System.Text.RegularExpressions;

namespace NetLab.Api.Middleware;

/// <summary>Đọc (hoặc tạo) X-Correlation-ID, trả lại trong response và đưa vào log scope.</summary>
public partial class CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
{
    public const string HeaderName = "X-Correlation-ID";

    [GeneratedRegex(@"^[A-Za-z0-9\-_]{1,64}$")]
    private static partial Regex ValidId();

    public async Task InvokeAsync(HttpContext context)
    {
        var id = context.Request.Headers[HeaderName].FirstOrDefault();
        if (string.IsNullOrEmpty(id) || !ValidId().IsMatch(id))
            id = Guid.NewGuid().ToString("N");

        context.Items[HeaderName] = id;
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = id;
            return Task.CompletedTask;
        });

        using (logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = id }))
        {
            await next(context);
        }
    }
}
