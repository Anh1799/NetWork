namespace NetLab.Api.Services;

/// <summary>Lỗi nghiệp vụ kèm HTTP status code, được middleware chuyển thành ProblemDetails.</summary>
public class AppException(int statusCode, string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}
