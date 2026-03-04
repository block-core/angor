<<<<<<<< HEAD:src/Angor/Shared/Integration/Lightning/BoltzWebSocketClient.cs
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Angor.Shared.Integration.Lightning.Models;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;

namespace Angor.Shared.Integration.Lightning;

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

    public BoltzWebSocketClient(
        BoltzConfiguration configuration,
        ILogger<BoltzWebSocketClient> logger)
    {
        var baseUrl = configuration.BaseUrl
            .Replace("https://", "wss://")
            .Replace("http://", "ws://")
            .TrimEnd('/');

        var wsPath = configuration.UseV2Prefix ? "/v2/ws" : "/ws";
        _webSocketUrl = $"{baseUrl}{wsPath}";

        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

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

            await SubscribeToSwap(swapId, linkedCts.Token);

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
            _logger.LogDebug("Received WebSocket message: {Message}", message);

            using var doc = JsonDocument.Parse(message);
            var root = doc.RootElement;

            if (!root.TryGetProperty("event", out var eventProp) ||
                eventProp.GetString() != "update")
            {
                _logger.LogDebug("Ignoring non-update event: {Event}",
                    eventProp.ValueKind == JsonValueKind.String ? eventProp.GetString() : "unknown");
                return null;
            }

            if (!root.TryGetProperty("args", out var argsProp) ||
                argsProp.ValueKind != JsonValueKind.Array ||
                argsProp.GetArrayLength() == 0)
            {
                _logger.LogDebug("No args in update message");
                return null;
            }

            var firstArg = argsProp[0];

            string? updateId = null;
            string? status = null;
            string? failureReason = null;
            string? transactionId = null;
            string? transactionHex = null;

            if (firstArg.TryGetProperty("id", out var idProp))
                updateId = idProp.GetString();

            if (firstArg.TryGetProperty("status", out var statusProp))
                status = statusProp.GetString();

            if (firstArg.TryGetProperty("failureReason", out var failureProp))
                failureReason = failureProp.GetString();

            if (firstArg.TryGetProperty("transaction", out var txProp) &&
                txProp.ValueKind == JsonValueKind.Object)
            {
                if (txProp.TryGetProperty("id", out var txIdProp))
                    transactionId = txIdProp.GetString();
                if (txProp.TryGetProperty("hex", out var txHexProp))
                    transactionHex = txHexProp.GetString();
            }

            if (string.IsNullOrEmpty(status))
            {
                _logger.LogWarning("WebSocket update missing status field. Message: {Message}", message);
                return null;
            }

            _logger.LogDebug("Parsed swap update - Id: {Id}, Status: {Status}, TxId: {TxId}",
                updateId ?? swapId, status, transactionId ?? "none");

            return new BoltzSwapStatus
            {
                SwapId = updateId ?? swapId,
                Status = ParseSwapState(status),
                TransactionId = transactionId,
                TransactionHex = transactionHex,
                FailureReason = failureReason
            };
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse WebSocket message as JSON. Message: {Message}", message);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Unexpected error processing WebSocket message: {Message}", message);
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
}

========
// Moved to Angor.Shared.Integration.Lightning
>>>>>>>> e7fcac64 (Refactor Boltz integration: move DTOs to Angor.Shared.Integration.Lightning and implement XunitLogger for test output):src/Angor/Avalonia/Angor.Sdk/Integration/Lightning/BoltzWebSocketClient.cs
