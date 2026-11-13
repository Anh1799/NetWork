using System.Net.Sockets;

namespace NetLab.Api.Services;

/// <summary>
/// SocketsHttpHandler kiểm tra địa chỉ IP NGAY LÚC KẾT NỐI (chống DNS rebinding)
/// và không tự theo redirect (chống redirect tới địa chỉ nội bộ).
/// </summary>
public static class GuardedHandler
{
    public static SocketsHttpHandler Create(IHostGuard guard) => new()
    {
        AllowAutoRedirect = false,
        ConnectTimeout = TimeSpan.FromSeconds(10),
        PooledConnectionLifetime = TimeSpan.FromMinutes(2),
        ConnectCallback = async (context, ct) =>
        {
            var addresses = await guard.ResolveAllowedAsync(context.DnsEndPoint.Host, ct);

            var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
            try
            {
                await socket.ConnectAsync(addresses, context.DnsEndPoint.Port, ct);
                return new NetworkStream(socket, ownsSocket: true);
            }
            catch
            {
                socket.Dispose();
                throw;
            }
        }
    };
}
