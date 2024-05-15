using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace IT3B_Chat.Server
{
    internal class WebsocketServer
    {
        public class WebSocketServer
        {
            private readonly IPAddress _ipAddress;
            private readonly int _port;
            private readonly HttpListener _httpListener;
            private readonly List<WebSocket> _clients;

            public WebSocketServer(IPAddress ipAddress, int port)
            {
                _ipAddress = ipAddress;
                _port = port;
                _httpListener = new HttpListener();
                _clients = new List<WebSocket>();
            }

            public async Task StartAsync()
            {
                _httpListener.Prefixes.Add($"http://{_ipAddress}:{_port}/");
                _httpListener.Start();
                Console.WriteLine($"WebSocket server started at ws://{_ipAddress}:{_port}/");

                while (true)
                {
                    var httpContext = await _httpListener.GetContextAsync();

                    if (httpContext.Request.IsWebSocketRequest)
                    {
                        ProcessRequest(httpContext);
                    }
                    else
                    {
                        httpContext.Response.StatusCode = 400;
                        httpContext.Response.Close();
                    }
                }
            }

            private async void ProcessRequest(HttpListenerContext httpContext)
            {
                WebSocketContext webSocketContext = null;

                try
                {
                    webSocketContext = await httpContext.AcceptWebSocketAsync(null);
                    Console.WriteLine("Connected: " + httpContext.Request.RemoteEndPoint);
                    lock (_clients)
                    {
                        _clients.Add(webSocketContext.WebSocket);
                    }
                }
                catch (Exception e)
                {
                    httpContext.Response.StatusCode = 500;
                    httpContext.Response.Close();
                    Console.WriteLine("Exception: " + e);
                    return;
                }

                WebSocket webSocket = webSocketContext.WebSocket;

                try
                {
                    while (webSocket.State == WebSocketState.Open)
                    {
                        var buffer = new byte[1024];
                        var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

                        if (result.MessageType == WebSocketMessageType.Close)
                        {
                            await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
                            lock (_clients)
                            {
                                _clients.Remove(webSocket);
                            }
                            Console.WriteLine("Disconnected: " + httpContext.Request.RemoteEndPoint);
                        }
                        else
                        {
                            var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                            Console.WriteLine("Received: " + message);

                            lock (_clients)
                            {
                                foreach (var client in _clients)
                                {
                                    if (client.State == WebSocketState.Open)
                                    {
                                        var msgBuffer = Encoding.UTF8.GetBytes(message);
                                        client.SendAsync(new ArraySegment<byte>(msgBuffer), WebSocketMessageType.Text, true, CancellationToken.None);
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine("Exception: " + e);
                    lock (_clients)
                    {
                        _clients.Remove(webSocket);
                    }
                    webSocket.Dispose();
                }
            }
        }
    }
}
