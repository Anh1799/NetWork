using System.Net.WebSockets;

namespace NetLab.Api.WebSockets;

/// <summary>WebSocket đơn giản nhất: gửi gì nhận lại đó.</summary>
public static class EchoWebSocket
{
    public static async Task HandleAsync(HttpContext context)
    {
        if (!context.WebSockets.IsWebSocketRequest)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsync("This endpoint requires a WebSocket connection.");
            return;
        }

        using var socket = await context.WebSockets.AcceptWebSocketAsync();
        var buffer = new byte[4096];

        try
        {
            while (socket.State == WebSocketState.Open)
            {
                var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), context.RequestAborted);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", CancellationToken.None);
                    break;
                }

                await socket.SendAsync(new ArraySegment<byte>(buffer, 0, result.Count),
                    result.MessageType, result.EndOfMessage, context.RequestAborted);
            }
        }
        catch (Exception ex) when (ex is OperationCanceledException or WebSocketException)
        {
            // Client ngắt kết nối đột ngột - bình thường
        }
    }
}
