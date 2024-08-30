namespace Angor.Shared.Models;

public class SettingsInfo
{
    public List<SettingsUrl> Indexers { get; set; } = new();
    public List<SettingsUrl> Relays { get; set; } = new();

    public List<WebSocketOption> WebSocketOptions { get; set; } =
        WebSocketServiceOptions.GetAvailableWebSocketOptions();

    public string SelectedWebSocketUrl { get; set; } =
        WebSocketServiceOptions.GetAvailableWebSocketOptions().FirstOrDefault()?.Url ?? string.Empty;
}

public class SettingsUrl
{
    public string Name { get; set; }
    public string Url { get; set; }

    public bool IsPrimary { get; set; }

    public UrlStatus Status { get; set; }

    public DateTime LastCheck { get; set; }
}

public enum UrlStatus
{
    Offline,
    NotReady,
    Online
}

public static class WebSocketServiceOptions
{
    public static List<WebSocketOption> GetAvailableWebSocketOptions()
    {
        return new List<WebSocketOption>
        {
            new() { Name = "Binance (BTC/USDT)", Url = "wss://stream.binance.com:9443/ws/btcusdt@trade" },
            new() { Name = "Bitfinex (BTC/USD)", Url = "wss://api-pub.bitfinex.com/ws/2" },
            new() { Name = "Kraken (BTC/USD)", Url = "wss://ws.kraken.com" }
            // Add more WebSocket URLs for different cryptocurrency exchanges or pairs
        };
    }
}

public class WebSocketOption
{
    public string Name { get; set; } // Display name for the dropdown
    public string Url { get; set; } // Actual WebSocket URL
}