namespace TaskFlow.Api.Services;

/// <summary>Lỗi nghiệp vụ có HTTP status code đi kèm, được middleware chuyển thành ProblemDetails.</summary>
public class AppException(int statusCode, string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}
