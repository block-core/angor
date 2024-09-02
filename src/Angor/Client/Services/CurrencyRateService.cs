using System.Text.Json;
using Angor.Client.Storage;

public class CurrencyRateService : ICurrencyRateService
{
    private readonly TimeSpan _cacheDuration = TimeSpan.FromMinutes(10); // Cache duration of 10 minutes
    private readonly ICacheStorage _cacheStorage;
    private readonly HttpClient _httpClient;
    private readonly ILogger<CurrencyRateService> _logger;

    public CurrencyRateService(HttpClient httpClient, ILogger<CurrencyRateService> logger, ICacheStorage cacheStorage)
    {
        _httpClient = httpClient;
        _logger = logger;
        _cacheStorage = cacheStorage; 
    }

    public async Task<decimal> GetBtcToCurrencyRate(string currencyCode)
    {
        var preferredCurrency = currencyCode.ToUpper();

        // Try to get the cached rate from ICacheStorage
        var cachedEntry = _cacheStorage.GetCurrencyRate(preferredCurrency);
        if (cachedEntry != null && DateTime.UtcNow - cachedEntry.Timestamp < _cacheDuration)
        {
            _logger.LogInformation($"Using cached rate for {preferredCurrency}");
            return cachedEntry.Rate;
        }

        // Fetch new rate if not cached
        var apiUrl = "https://mempool.space/api/v1/prices";
        var response = await _httpClient.GetAsync(apiUrl);
        if (response.IsSuccessStatusCode)
        {
            var jsonString = await response.Content.ReadAsStringAsync();
            _logger.LogInformation($"API Response: {jsonString}");

            var data = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(jsonString);

            if (data != null && data.ContainsKey(preferredCurrency))
            {
                var rate = data[preferredCurrency].GetDecimal();

                _cacheStorage.SetCurrencyRate(preferredCurrency,
                    new RateCacheEntry { Rate = rate, Timestamp = DateTime.UtcNow });
                return rate;
            }

            throw new Exception($"Currency '{preferredCurrency}' not found in the API response.");
        }

        throw new Exception("Failed to fetch BTC rate from the API.");
    }
}

public class RateCacheEntry
{
    public decimal Rate { get; set; }
    public DateTime Timestamp { get; set; }
}