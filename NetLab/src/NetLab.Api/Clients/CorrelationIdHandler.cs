using NetLab.Api.Middleware;

namespace NetLab.Api.Clients;

/// <summary>DelegatingHandler: gắn X-Correlation-ID của request đang xử lý vào mọi request gửi ra ngoài.</summary>
public class CorrelationIdHandler(IHttpContextAccessor accessor) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (accessor.HttpContext?.Items[CorrelationIdMiddleware.HeaderName] is string id &&
            !request.Headers.Contains(CorrelationIdMiddleware.HeaderName))
        {
            request.Headers.TryAddWithoutValidation(CorrelationIdMiddleware.HeaderName, id);
        }

        return base.SendAsync(request, cancellationToken);
    }
}
