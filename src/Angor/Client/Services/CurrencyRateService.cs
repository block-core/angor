using System.Collections.Concurrent;
using System.Text.Json;

public class CurrencyRateService : ICurrencyRateService
{
    private readonly TimeSpan _cacheDuration = TimeSpan.FromMinutes(10); // Cache duration of 10 minutes
    private readonly HttpClient _httpClient;
    private readonly ILogger<CurrencyRateService> _logger;
    private readonly ConcurrentDictionary<string, (decimal rate, DateTime timestamp)> _rateCache = new();

    public CurrencyRateService(HttpClient httpClient, ILogger<CurrencyRateService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<decimal> GetBtcToCurrencyRate(string currencyCode)
    {
        // Normalize currency code
        var preferredCurrency = currencyCode.ToUpper();

        // Check if we have a recent rate in the cache
        if (_rateCache.ContainsKey(preferredCurrency) &&
            DateTime.UtcNow - _rateCache[preferredCurrency].timestamp < _cacheDuration)
        {
            _logger.LogInformation($"Using cached rate for {preferredCurrency}");
            return _rateCache[preferredCurrency].rate;
        }

        // Fetch new rate if not cached or cache is stale
        var apiUrl = "https://mempool.space/api/v1/prices";
        var response = await _httpClient.GetAsync(apiUrl);
        if (response.IsSuccessStatusCode)
        {
            var jsonString = await response.Content.ReadAsStringAsync();
            _logger.LogInformation($"API Response: {jsonString}");

            var data = JsonSerializer.Deserialize<Dictionary<string, decimal>>(jsonString);

            if (data != null && data.ContainsKey(preferredCurrency))
            {
                var rate = data[preferredCurrency];
                // Update the cache
                _rateCache[preferredCurrency] = (rate, DateTime.UtcNow);
                return rate;
            }

            throw new Exception($"Currency '{preferredCurrency}' not found in the API response.");
        }

        throw new Exception("Failed to fetch BTC rate from the API.");
    }
}