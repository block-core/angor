public interface ICurrencyRateService
{
    // Property to expose WebSocket connection status
    string WebSocketStatus { get; }
    Task<decimal> GetBtcToCurrencyRate(string currencyCode);
}