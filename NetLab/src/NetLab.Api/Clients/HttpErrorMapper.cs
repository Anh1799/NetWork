using Polly.CircuitBreaker;
using Polly.Timeout;
using NetLab.Api.Services;

namespace NetLab.Api.Clients;

/// <summary>Chuyển lỗi mạng khi gọi dịch vụ bên ngoài thành HTTP status phù hợp (502/504).</summary>
public static class HttpErrorMapper
{
    public static bool TryMap(Exception ex, string service, ILogger logger, out AppException? mapped)
    {
        switch (ex)
        {
            case TimeoutRejectedException:
                logger.LogWarning(ex, "{Service} timed out.", service);
                mapped = new AppException(StatusCodes.Status504GatewayTimeout, $"The {service} service timed out.");
                return true;

            case HttpRequestException or BrokenCircuitException:
                logger.LogWarning(ex, "{Service} is unavailable.", service);
                mapped = new AppException(StatusCodes.Status502BadGateway, $"The {service} service is currently unavailable.");
                return true;

            default:
                mapped = null;
                return false;
        }
    }
}
