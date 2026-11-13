using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using NetLab.Api.Services;

namespace NetLab.Api.Middleware;

/// <summary>Bắt mọi exception và trả về ProblemDetails (RFC 7807).</summary>
public class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (AppException ex)
        {
            await WriteProblemAsync(context, ex.StatusCode, ex.Message);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            if (!context.Response.HasStarted) context.Response.StatusCode = 499; // client đóng kết nối
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception for {Method} {Path}", context.Request.Method, context.Request.Path);
            await WriteProblemAsync(context, StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
        }
    }

    private static Task WriteProblemAsync(HttpContext context, int status, string detail)
    {
        if (context.Response.HasStarted) return Task.CompletedTask;

        context.Response.StatusCode = status;

        var problem = new ProblemDetails
        {
            Status = status,
            Title = ReasonPhrases.GetReasonPhrase(status),
            Detail = detail,
            Instance = context.Request.Path
        };
        if (context.Items[CorrelationIdMiddleware.HeaderName] is string id)
            problem.Extensions["correlationId"] = id;

        return context.Response.WriteAsJsonAsync(problem, (JsonSerializerOptions?)null, "application/problem+json");
    }
}
