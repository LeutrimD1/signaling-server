using Fleck;
using System.Text.Json;
using System.Collections.Concurrent;

namespace WebRTCServer
{
    public class SocketInfo
    {
        public required IWebSocketConnection Connection { get; set; }
        public string Offer { get; set; } = "";
        public string Answer { get; set; } = "";
        public string TargetSocketGuid { get; set; } = "";
    }

    public class Program
    {
        static void Main()
        {
            var sockets = new ConcurrentDictionary<Guid, SocketInfo>();

            void Pong()
            {
                var pong = new
                {
                    type = "Pong"
                };

                var response = JsonSerializer.Serialize(pong);

            }
            void BroadcastServerState()
            {
                var state = new
                {
                    sockets = sockets.Select(kv => new
                    {
                        socketGuid = kv.Key.ToString(),
                        offer = kv.Value.Offer,
                        answer = kv.Value.Answer,
                        targetSocketGuid = kv.Value.TargetSocketGuid
                    }).ToArray()
                };

                var json = JsonSerializer.Serialize(state);
                foreach (var s in sockets.Values)
                    s.Connection.Send(json);
            }

            var server = new WebSocketServer("ws://0.0.0.0:8181");
            server.Start(socket =>
            {
                Guid id = Guid.NewGuid();

                socket.OnOpen = () =>
                {
                    sockets[id] = new SocketInfo { Connection = socket };
                    Console.WriteLine($"Socket {id} connected");
                    BroadcastServerState();
                };

                socket.OnClose = () =>
                {
                    sockets.TryRemove(id, out _);
                    Console.WriteLine($"Socket {id} disconnected");
                    BroadcastServerState();
                };

                socket.OnMessage = msg =>
                {
                    try
                    {
                        var root = JsonDocument.Parse(msg).RootElement;

                        if (root.TryGetProperty("type", out var ping) && ping.ToString() == "ping")
                        {
                            socket.Send(JsonSerializer.Serialize(new { type = "pong" }));
                            return;
                        }

                        if (sockets.TryGetValue(id, out var socketInfo))
                        {
                            if (root.TryGetProperty("offer", out var offerProp))
                                socketInfo.Offer = offerProp.GetString() ?? "";

                            if (root.TryGetProperty("answer", out var answerProp))
                                socketInfo.Answer = answerProp.GetString() ?? "";

                            if (root.TryGetProperty("targetSocketGuid", out var targetProp))
                                socketInfo.TargetSocketGuid = targetProp.GetString() ?? "";

                            BroadcastServerState();
                        }
                    }
                    catch (JsonException)
                    {
                        // ignore invalid JSON
                    }
                };
            });

            Console.WriteLine("WebSocket server started on ws://0.0.0.0:8181");
            new System.Threading.ManualResetEvent(false).WaitOne();
        }
    }
}
