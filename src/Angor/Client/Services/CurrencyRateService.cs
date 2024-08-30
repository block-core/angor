using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using Angor.Client.Storage;
using Microsoft.JSInterop;

public class CurrencyRateService : ICurrencyRateService, IAsyncDisposable
{
    private readonly DotNetObjectReference<CurrencyRateService> _dotNetRef;
    private readonly IJSRuntime _jsRuntime;
    private readonly ILogger<CurrencyRateService> _logger;
    private readonly ConcurrentDictionary<string, decimal> _rateCache = new(); // Cache for storing currency rates
    private readonly IClientStorage _storage;

    public CurrencyRateService(IClientStorage storage, ILogger<CurrencyRateService> logger, IJSRuntime jsRuntime)
    {
        _storage = storage;
        _logger = logger;
        _jsRuntime = jsRuntime;
        _dotNetRef = DotNetObjectReference.Create(this);
        InitializeWebSocketConnection();
    }

    public async ValueTask DisposeAsync()
    {
        if (_dotNetRef != null) _dotNetRef.Dispose();

        await _jsRuntime.InvokeVoidAsync("webSocketInterop.disconnect");
    }

    public string WebSocketStatus { get; private set; } = "Disconnected";

    public Task<decimal> GetBtcToCurrencyRate(string currencyCode)
    {
        if (_rateCache.TryGetValue(currencyCode.ToUpper(), out var rate)) return Task.FromResult(rate);

        _logger.LogWarning($"Rate for currency '{currencyCode}' not found.");
        throw new Exception($"Rate for currency '{currencyCode}' not found.");
    }

    private void InitializeWebSocketConnection()
    {
        var proxyWebSocketUrl = "ws://localhost:5063/api/WebSocketProxy/binance"; // Use your backend proxy URL

        _jsRuntime.InvokeVoidAsync("webSocketInterop.connect", proxyWebSocketUrl, _dotNetRef);
        WebSocketStatus = "Connecting";
        _logger.LogInformation($"Connecting to WebSocket: {proxyWebSocketUrl}");
    }

    [JSInvokable]
    public void OnWebSocketMessage(string message)
    {
        _logger.LogInformation($"Received WebSocket message: {message}");

        try
        {
            // Deserialize the incoming message into a TradeMessage object with specific number handling options
            var options = new JsonSerializerOptions
            {
                NumberHandling = JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString
            };

            var tradeMessage = JsonSerializer.Deserialize<TradeMessage>(message, options);

            if (tradeMessage != null)
            {
                var rateKey = tradeMessage.s; // Use the symbol (s) as the key
                var rateValue = tradeMessage.p; // Use the price (p) as the rate

                _rateCache[rateKey] = rateValue; // Update the cache with the parsed rate
                _logger.LogInformation($"Updated rate for {rateKey}: {rateValue}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Failed to handle WebSocket message: {ex.Message}");
        }
    }


    [JSInvokable("OnWebSocketError")] // Explicitly specify a unique name
    public void OnWebSocketError(string errorMessage)
    {
        WebSocketStatus = "Error";
        _logger.LogError($"WebSocket error: {errorMessage}");
    }

    [JSInvokable("OnWebSocketClose")] // Explicitly specify a unique name
    public void OnWebSocketClose()
    {
        WebSocketStatus = "Disconnected";
        _logger.LogInformation("WebSocket connection closed.");
    }
}

// Custom JsonConverter for decimal to handle possible conversion issues
public class DecimalConverter : JsonConverter<decimal>
{
    public override decimal Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            var stringValue = reader.GetString();
            if (decimal.TryParse(stringValue, out var value)) return value;
            throw new JsonException($"Unable to parse '{stringValue}' to a decimal.");
        }

        return reader.GetDecimal();
    }

    public override void Write(Utf8JsonWriter writer, decimal value, JsonSerializerOptions options)
    {
        writer.WriteNumberValue(value);
    }
}

public class TradeMessage
{
    public string e { get; set; } // Event type
    public long E { get; set; } // Event time
    public string s { get; set; } // Symbol
    public long t { get; set; } // Trade ID
    public decimal p { get; set; } // Price
    public decimal q { get; set; } // Quantity
    public long T { get; set; } // Trade time
    public bool m { get; set; } // Is the buyer the market maker?
    public bool M { get; set; } // Ignore
}