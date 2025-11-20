using Fleck;
using System.Text.Json;

var server = new WebSocketServer("ws://0.0.0.0:8181");
var sockets = new Dictionary<Guid, IWebSocketConnection>();

var broadcast = () =>
{
    var response = new
    {
        type = "connections",
        connectedIds = sockets.Keys.Select(k => k.ToString()).ToArray(),
        count = sockets.Count
    };
    
    var json = JsonSerializer.Serialize(response);
    foreach (var s in sockets.Values)
        s.Send(json);
};

server.Start(socket =>
{
    Guid id = Guid.NewGuid();

    socket.OnMessage = msg =>
    {
        try
        {
            var root = JsonDocument.Parse(msg).RootElement;
            
            if (root.TryGetProperty("type", out var typeProperty))
            {
                var messageType = typeProperty.GetString();
                
                switch (messageType)
                {
                    case "ping":
                        var pongResponse = new
                        {
                            type = "pong",
                            timestamp = DateTime.UtcNow.ToString("o")
                        };
                        socket.Send(JsonSerializer.Serialize(pongResponse));
                        break;
                        
                    case "message":
                        if (root.TryGetProperty("content", out var content))
                        {
                            var echoResponse = new
                            {
                                type = "echo",
                                originalMessage = content.GetString(),
                                socketId = id.ToString()
                            };
                            socket.Send(JsonSerializer.Serialize(echoResponse));
                        }
                        break;
                        
                    default:
                        var errorResponse = new
                        {
                            type = "error",
                            message = $"Unknown message type: {messageType}"
                        };
                        socket.Send(JsonSerializer.Serialize(errorResponse));
                        break;
                }
            }
            else
            {
                var errorResponse = new
                {
                    type = "error",
                    message = "Message must have a 'type' property"
                };
                socket.Send(JsonSerializer.Serialize(errorResponse));
            }
        }
        catch (JsonException ex)
        {
            var errorResponse = new
            {
                type = "error",
                message = $"Invalid JSON: {ex.Message}"
            };
            socket.Send(JsonSerializer.Serialize(errorResponse));
        }
    };

    socket.OnOpen = () =>
    {
        sockets[id] = socket;
        Console.WriteLine($"Socket {id} connected");
        broadcast();
    };

    socket.OnClose = () =>
    {
        sockets.Remove(id);
        Console.WriteLine($"Socket {id} disconnected");
        broadcast();
    };
});

Console.WriteLine("WebSocket server started on ws://0.0.0.0:8181");
new ManualResetEvent(false).WaitOne();