using System.Net.Http.Json;
using NetLab.Api.Services;

namespace NetLab.Api.Clients;

public interface IPublicIpClient
{
    Task<string> GetPublicIpAsync(CancellationToken ct = default);
}

/// <summary>Hỏi dịch vụ ipify xem IP public của SERVER đang là gì.</summary>
public class PublicIpClient(HttpClient http, ILogger<PublicIpClient> logger) : IPublicIpClient
{
    private sealed record IpifyResponse(string Ip);

    public async Task<string> GetPublicIpAsync(CancellationToken ct = default)
    {
        try
        {
            var result = await http.GetFromJsonAsync<IpifyResponse>("?format=json", ct);
            return result?.Ip ?? throw new AppException(StatusCodes.Status502BadGateway, "Unexpected response from IP service.");
        }
        catch (Exception ex) when (HttpErrorMapper.TryMap(ex, "public IP", logger, out var mapped))
        {
            throw mapped!;
        }
    }
}
