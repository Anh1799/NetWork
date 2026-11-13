using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using NetLab.Api.Configuration;

namespace NetLab.Api.Services;

/// <summary>
/// Chống SSRF: API này kết nối mạng thay cho người gọi, nên phải chặn địa chỉ nội bộ
/// (loopback, private, link-local như 169.254.169.254 của cloud metadata, multicast...).
/// </summary>
public interface IHostGuard
{
    bool IsAddressAllowed(IPAddress address);
    string NormalizeHost(string host);
    Task<IPAddress[]> ResolveAsync(string host, CancellationToken ct);
    Task<IPAddress[]> ResolveAllowedAsync(string host, CancellationToken ct);
}

public partial class HostGuard(IOptions<NetworkOptions> options) : IHostGuard
{
    [GeneratedRegex(@"^[A-Za-z0-9.\-:]{1,253}$")]
    private static partial Regex HostRegex();

    public string NormalizeHost(string host)
    {
        host = (host ?? string.Empty).Trim();
        if (!HostRegex().IsMatch(host))
            throw new AppException(StatusCodes.Status400BadRequest, "Invalid host name.");
        return host;
    }

    public bool IsAddressAllowed(IPAddress address)
    {
        if (options.Value.AllowPrivateAddresses) return true;

        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
        if (IPAddress.IsLoopback(address)) return false;

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            if (address.IsIPv6LinkLocal || address.IsIPv6SiteLocal || address.IsIPv6Multicast ||
                address.Equals(IPAddress.IPv6Any) || address.Equals(IPAddress.IPv6None))
                return false;

            var v6 = address.GetAddressBytes();
            return (v6[0] & 0xFE) != 0xFC; // fc00::/7 unique local
        }

        var b = address.GetAddressBytes();
        var blocked =
            b[0] == 0 ||                                  // 0.0.0.0/8
            b[0] == 10 ||                                 // 10.0.0.0/8
            b[0] == 127 ||                                // loopback
            (b[0] == 100 && b[1] is >= 64 and <= 127) ||  // 100.64.0.0/10 (CGNAT)
            (b[0] == 169 && b[1] == 254) ||               // link-local / cloud metadata
            (b[0] == 172 && b[1] is >= 16 and <= 31) ||   // 172.16.0.0/12
            (b[0] == 192 && b[1] == 168) ||               // 192.168.0.0/16
            (b[0] == 192 && b[1] == 0 && b[2] == 0) ||    // 192.0.0.0/24
            (b[0] == 198 && b[1] is 18 or 19) ||          // benchmark
            b[0] >= 224;                                  // multicast + reserved + broadcast
        return !blocked;
    }

    public async Task<IPAddress[]> ResolveAsync(string host, CancellationToken ct)
    {
        try
        {
            var addresses = await Dns.GetHostAddressesAsync(host, ct);
            if (addresses.Length == 0)
                throw new AppException(StatusCodes.Status404NotFound, $"Cannot resolve host '{host}'.");
            return addresses;
        }
        catch (SocketException)
        {
            throw new AppException(StatusCodes.Status404NotFound, $"Cannot resolve host '{host}'.");
        }
    }

    public async Task<IPAddress[]> ResolveAllowedAsync(string host, CancellationToken ct)
    {
        var allowed = (await ResolveAsync(host, ct)).Where(IsAddressAllowed).ToArray();
        if (allowed.Length == 0)
            throw new AppException(StatusCodes.Status403Forbidden,
                "Host resolves to a private, loopback or reserved address, which is blocked.");
        return allowed;
    }
}
