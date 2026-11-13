using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;

namespace NetLab.Api.WebSockets;

/// <summary>Phòng chat quảng bá (broadcast) qua WebSocket, lưu trong bộ nhớ.</summary>
public class ChatHub
{
    private const int MaxMessageBytes = 4096;

    // WebSocket chỉ cho phép MỘT thao tác gửi tại một thời điểm -> mỗi client có một semaphore
    private sealed record Client(string Name, WebSocket Socket, SemaphoreSlim SendLock);

    private readonly ConcurrentDictionary<Guid, Client> _clients = new();

    public int ConnectedCount => _clients.Count;

    public async Task HandleAsync(HttpContext context)
    {
        if (!context.WebSockets.IsWebSocketRequest)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsync("This endpoint requires a WebSocket connection.");
            return;
        }

        using var socket = await context.WebSockets.AcceptWebSocketAsync();

        var id = Guid.NewGuid();
        var name = SanitizeName(context.Request.Query["name"].ToString(), id);
        var client = new Client(name, socket, new SemaphoreSlim(1, 1));

        _clients[id] = client;
        await BroadcastAsync($"* {name} joined ({_clients.Count} online)");

        try
        {
            while (socket.State == WebSocketState.Open)
            {
                var text = await ReceiveTextAsync(socket, context.RequestAborted);
                if (text is null) break;
                if (text.Length == 0) continue;

                await BroadcastAsync($"{name}: {text}");
            }
        }
        catch (Exception ex) when (ex is OperationCanceledException or WebSocketException)
        {
            // Client rời đi đột ngột
        }
        finally
        {
            _clients.TryRemove(id, out _);
            await BroadcastAsync($"* {name} left ({_clients.Count} online)");

            if (socket.State is WebSocketState.Open or WebSocketState.CloseReceived)
            {
                try { await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", CancellationToken.None); }
                catch (WebSocketException) { }
            }
        }
    }

    /// <summary>Đọc một tin nhắn text hoàn chỉnh (có thể nhiều frame). Trả về null nếu client đóng kết nối.</summary>
    private static async Task<string?> ReceiveTextAsync(WebSocket socket, CancellationToken ct)
    {
        var buffer = new byte[1024];
        using var message = new MemoryStream();

        WebSocketReceiveResult result;
        do
        {
            result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), ct);

            if (result.MessageType == WebSocketMessageType.Close) return null;

            message.Write(buffer, 0, result.Count);
            if (message.Length > MaxMessageBytes)
            {
                await socket.CloseAsync(WebSocketCloseStatus.MessageTooBig, "Message too large", CancellationToken.None);
                return null;
            }
        }
        while (!result.EndOfMessage);

        return result.MessageType == WebSocketMessageType.Text
            ? Encoding.UTF8.GetString(message.ToArray()).Trim()
            : string.Empty; // bỏ qua binary
    }

    private async Task BroadcastAsync(string message)
    {
        var bytes = Encoding.UTF8.GetBytes(message);

        foreach (var (id, client) in _clients)
        {
            if (client.Socket.State != WebSocketState.Open) continue;

            await client.SendLock.WaitAsync();
            try
            {
                await client.Socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
            }
            catch (Exception ex) when (ex is WebSocketException or ObjectDisposedException or InvalidOperationException)
            {
                _clients.TryRemove(id, out _);
            }
            finally
            {
                client.SendLock.Release();
            }
        }
    }

    private static string SanitizeName(string? raw, Guid id)
    {
        var cleaned = new string((raw ?? string.Empty).Where(c => char.IsLetterOrDigit(c) || c is '_' or '-').Take(20).ToArray());
        return cleaned.Length > 0 ? cleaned : "guest-" + id.ToString("N")[..4];
    }
}
