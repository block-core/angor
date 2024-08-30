using System.Net.WebSockets;
using Microsoft.AspNetCore.Mvc;

[Route("api/[controller]")]
[ApiController]
public class WebSocketProxyController : ControllerBase
{
    [HttpGet("binance")]
    public async Task Get()
    {
        if (HttpContext.WebSockets.IsWebSocketRequest)
        {
            using var clientWebSocket = new ClientWebSocket();
            await clientWebSocket.ConnectAsync(new Uri("wss://stream.binance.com:9443/ws/btcusdt@trade"),
                CancellationToken.None);

            using var serverWebSocket = await HttpContext.WebSockets.AcceptWebSocketAsync();

            var buffer = new byte[4096];
            var receiveTask = Task.Run(async () =>
            {
                while (clientWebSocket.State == WebSocketState.Open)
                {
                    var result =
                        await clientWebSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                    if (result.MessageType == WebSocketMessageType.Close)
                        await clientWebSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing",
                            CancellationToken.None);
                    else
                        await serverWebSocket.SendAsync(new ArraySegment<byte>(buffer, 0, result.Count),
                            result.MessageType, result.EndOfMessage, CancellationToken.None);
                }
            });

            var sendTask = Task.Run(async () =>
            {
                while (serverWebSocket.State == WebSocketState.Open)
                {
                    var result =
                        await serverWebSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                    if (result.MessageType == WebSocketMessageType.Close)
                        await serverWebSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing",
                            CancellationToken.None);
                    else
                        await clientWebSocket.SendAsync(new ArraySegment<byte>(buffer, 0, result.Count),
                            result.MessageType, result.EndOfMessage, CancellationToken.None);
                }
            });

            await Task.WhenAll(receiveTask, sendTask);
        }
        else
        {
            HttpContext.Response.StatusCode = 400;
        }
    }
}