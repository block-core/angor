using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Angor.Sdk.Integration.Lightning.Models;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;

namespace Angor.Sdk.Integration.Lightning;

/// <summary>
/// WebSocket client for real-time Boltz swap status updates.
/// Much more efficient than polling - receives push notifications when swap status changes.
/// </summary>
public class BoltzWebSocketClient : IBoltzWebSocketClient, IAsyncDisposable
{
    private readonly string _webSocketUrl;
    private readonly ILogger<BoltzWebSocketClient> _logger;
    private readonly JsonSerializerOptions _jsonOptions;
    private ClientWebSocket? _webSocket;
    private CancellationTokenSource? _receiveCts;

    public BoltzWebSocketClient(
        BoltzConfiguration configuration,
        ILogger<BoltzWebSocketClient> logger)
    {
        // Convert HTTP URL to WebSocket URL
        // https://api.boltz.exchange -> wss://api.boltz.exchange/v2/ws (if UseV2Prefix)
        // http://localhost:9001 -> ws://localhost:9001/v2/ws (if UseV2Prefix)
        var baseUrl = configuration.BaseUrl
            .Replace("https://", "wss://")
            .Replace("http://", "ws://")
            .TrimEnd('/');
        
        // Use the same prefix configuration as HTTP API
        var wsPath = configuration.UseV2Prefix ? "/v2/ws" : "/ws";
        _webSocketUrl = $"{baseUrl}{wsPath}";
        
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    /// <summary>
    /// Monitors a swap via WebSocket until it reaches a terminal state.
    /// Returns when the swap completes, fails, or times out.
    /// </summary>
    public async Task<Result<BoltzSwapStatus>> MonitorSwapAsync(
        string swapId,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        var effectiveTimeout = timeout ?? TimeSpan.FromMinutes(30);
        using var timeoutCts = new CancellationTokenSource(effectiveTimeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken, timeoutCts.Token);

        try
        {
            _webSocket = new ClientWebSocket();
            await _webSocket.ConnectAsync(new Uri(_webSocketUrl), linkedCts.Token);
            
            _logger.LogInformation("Connected to Boltz WebSocket at {Url}", _webSocketUrl);

            // Subscribe to swap updates
            await SubscribeToSwap(swapId, linkedCts.Token);

            // Listen for updates
            return await ReceiveUpdatesUntilComplete(swapId, linkedCts.Token);
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
        {
            return Result.Failure<BoltzSwapStatus>(
                "Timeout waiting for swap completion. Please pay the Lightning invoice.");
        }
        catch (OperationCanceledException)
        {
            return Result.Failure<BoltzSwapStatus>("Monitoring was cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "WebSocket error monitoring swap {SwapId}", swapId);
            return Result.Failure<BoltzSwapStatus>($"WebSocket error: {ex.Message}");
        }
    }

    private async Task SubscribeToSwap(string swapId, CancellationToken cancellationToken)
    {
        var subscribeMessage = new
        {
            op = "subscribe",
            channel = "swap.update",
            args = new[] { swapId }
        };

        var json = JsonSerializer.Serialize(subscribeMessage, _jsonOptions);
        var bytes = Encoding.UTF8.GetBytes(json);
        
        await _webSocket!.SendAsync(
            new ArraySegment<byte>(bytes),
            WebSocketMessageType.Text,
            endOfMessage: true,
            cancellationToken);

        _logger.LogDebug("Subscribed to swap updates for {SwapId}", swapId);
    }

    private async Task<Result<BoltzSwapStatus>> ReceiveUpdatesUntilComplete(
        string swapId,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[4096];
        var messageBuilder = new StringBuilder();

        while (!cancellationToken.IsCancellationRequested && 
               _webSocket?.State == WebSocketState.Open)
        {
            var result = await _webSocket.ReceiveAsync(
                new ArraySegment<byte>(buffer), cancellationToken);

            if (result.MessageType == WebSocketMessageType.Close)
            {
                _logger.LogWarning("WebSocket closed by server");
                return Result.Failure<BoltzSwapStatus>("WebSocket connection closed");
            }

            messageBuilder.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));

            if (result.EndOfMessage)
            {
                var message = messageBuilder.ToString();
                messageBuilder.Clear();

                var status = ProcessMessage(swapId, message);
                
                if (status != null)
                {
                    _logger.LogInformation("Swap {SwapId} status: {Status}", swapId, status.Status);
                    
                    // Check for terminal states
                    if (status.Status.IsComplete() || status.Status.IsFailed() ||
                        status.Status == SwapState.TransactionMempool ||
                        status.Status == SwapState.TransactionConfirmed)
                    {
                        return Result.Success(status);
                    }
                }
            }
        }

        return Result.Failure<BoltzSwapStatus>("WebSocket connection ended unexpectedly");
    }

    private BoltzSwapStatus? ProcessMessage(string swapId, string message)
    {
        try
        {
            var wsMessage = JsonSerializer.Deserialize<WebSocketMessage>(message, _jsonOptions);
            
            if (wsMessage?.Event != "update" || wsMessage.Args == null || !wsMessage.Args.Any())
            {
                return null;
            }

            var update = wsMessage.Args[0];
            return new BoltzSwapStatus
            {
                SwapId = swapId,
                Status = ParseSwapState(update.Status),
                TransactionId = update.Transaction?.Id,
                TransactionHex = update.Transaction?.Hex,
                FailureReason = update.FailureReason
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse WebSocket message: {Message}", message);
            return null;
        }
    }

    private static SwapState ParseSwapState(string? status) => status switch
    {
        "swap.created" => SwapState.Created,
        "invoice.set" => SwapState.InvoiceSet,
        "invoice.pending" => SwapState.InvoicePaid,
        "invoice.paid" => SwapState.InvoicePaid,
        "invoice.failedToPay" => SwapState.InvoiceFailedToPay,
        "invoice.expired" => SwapState.InvoiceExpired,
        "transaction.mempool" => SwapState.TransactionMempool,
        "transaction.confirmed" => SwapState.TransactionConfirmed,
        "transaction.claimed" => SwapState.TransactionClaimed,
        "transaction.refunded" => SwapState.TransactionRefunded,
        "swap.expired" => SwapState.SwapExpired,
        _ => SwapState.Created
    };

    public async ValueTask DisposeAsync()
    {
        if (_webSocket != null)
        {
            if (_webSocket.State == WebSocketState.Open)
            {
                try
                {
                    await _webSocket.CloseAsync(
                        WebSocketCloseStatus.NormalClosure,
                        "Closing",
                        CancellationToken.None);
                }
                catch
                {
                    // Ignore close errors
                }
            }
            _webSocket.Dispose();
            _webSocket = null;
        }
    }

    #region DTOs

    private class WebSocketMessage
    {
        [JsonPropertyName("event")]
        public string? Event { get; set; }

        [JsonPropertyName("channel")]
        public string? Channel { get; set; }

        [JsonPropertyName("args")]
        public List<SwapUpdate>? Args { get; set; }
    }

    private class SwapUpdate
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("status")]
        public string? Status { get; set; }

        [JsonPropertyName("failureReason")]
        public string? FailureReason { get; set; }

        [JsonPropertyName("transaction")]
        public TransactionUpdate? Transaction { get; set; }
    }

    private class TransactionUpdate
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("hex")]
        public string? Hex { get; set; }
    }

    #endregion
}

