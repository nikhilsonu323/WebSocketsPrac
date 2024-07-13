using Microsoft.OpenApi.Validations;
using System;
using System.Collections.Concurrent;
using System.Diagnostics.Eventing.Reader;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using WebSocketPrac;

public static class WebSocketHandler
{
    private static ConcurrentDictionary<string, WebSocket> _sockets = new ConcurrentDictionary<string, WebSocket>();

    public static async Task HandleWebSocketAsync(HttpContext context, WebSocket webSocket)
    {
        string socketId = Guid.NewGuid().ToString();
        _sockets.TryAdd(socketId, webSocket);

        var userIdString = JsonSerializer.Serialize(new
        {
            userdId = socketId
        });

        await webSocket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(userIdString)), WebSocketMessageType.Text, true, CancellationToken.None);


        await NotifyClientsAsync();

        var buffer = new byte[1024 * 8];
        WebSocketReceiveResult result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

        while (!result.CloseStatus.HasValue)
        {
            var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
            Console.WriteLine($"Received: {message}");

            if (message.StartsWith("offer"))
            {
                var targetUserId = GetId(message);

                if (_sockets.TryGetValue(targetUserId, out var targetSocket) && targetSocket.State == WebSocketState.Open)
                {
                    var sendBuffer = Encoding.UTF8.GetBytes(SetId(message, socketId));
                    await targetSocket.SendAsync(new ArraySegment<byte>(sendBuffer), result.MessageType, result.EndOfMessage, CancellationToken.None);
                }
            }

            if (message.StartsWith("answer"))
            {
                var targetUserId = GetId(message);

                if (_sockets.TryGetValue(targetUserId, out var targetSocket) && targetSocket.State == WebSocketState.Open)
                {
                    var sendBuffer = Encoding.UTF8.GetBytes(SetId(message, socketId));
                    await targetSocket.SendAsync(new ArraySegment<byte>(sendBuffer), result.MessageType, result.EndOfMessage, CancellationToken.None);
                }
            }

            if (message.StartsWith("ice"))
            {
                var targetUserId = GetId(message);

                if (_sockets.TryGetValue(targetUserId, out var targetSocket) && targetSocket.State == WebSocketState.Open)
                {
                    var sendBuffer = Encoding.UTF8.GetBytes(SetId(message, socketId));
                    await targetSocket.SendAsync(new ArraySegment<byte>(sendBuffer), result.MessageType, result.EndOfMessage, CancellationToken.None);
                }
            }


            result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
        }
        Console.WriteLine("Closed connection");
        _sockets.TryRemove(socketId, out _);

        await webSocket.CloseAsync(result.CloseStatus.Value, result.CloseStatusDescription, CancellationToken.None);

        await NotifyClientsAsync();
    }

    private static async Task NotifyClientsAsync()
    {
        var userIds = _sockets.Keys;
        var userIdsString = JsonSerializer.Serialize(new
        {
            userdIds = userIds
        });

        foreach (var socket in _sockets.Values)
        {
            var encodedUserIds = Encoding.UTF8.GetBytes(userIdsString);
            if (socket.State == WebSocketState.Open)
            {
                await socket.SendAsync(new ArraySegment<byte>(encodedUserIds), WebSocketMessageType.Text, true, CancellationToken.None);
            }
        }
    }

    private async static Task SendSdp(string message, string socketId, WebSocketMessageType type,bool isEndOfMessage)
    {
        var targetUserId = GetId(message);

        if (_sockets.TryGetValue(targetUserId, out var targetSocket) && targetSocket.State == WebSocketState.Open)
        {
            var sendBuffer = Encoding.UTF8.GetBytes(SetId(message, socketId));
            await targetSocket.SendAsync(new ArraySegment<byte>(sendBuffer), type, isEndOfMessage, CancellationToken.None);
        }
    }

    private static string GetId(string message)
    {
        var splitIndex = message.IndexOf(':');
        var afterIdKey = message.Substring(splitIndex + 2);
        var lastIndex = afterIdKey.IndexOf(",");
        return message.Substring(splitIndex + 2, lastIndex - 1);
    }

    private static string SetId(string message, string socketId)
    {
        return "{\"id\":\"" + socketId + "\"," + message.Substring(message.IndexOf(",") + 1);
    }
}
