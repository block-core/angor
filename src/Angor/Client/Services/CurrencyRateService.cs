using System.Text.Json;
using Angor.Client.Storage;

public class CurrencyRateService : ICurrencyRateService
{
    private readonly TimeSpan _cacheDuration = TimeSpan.FromMinutes(10);
    private readonly ICacheStorage _cacheStorage;
    private readonly HttpClient _httpClient;
    private readonly ILogger<CurrencyRateService> _logger;

    private const string ApiUrl = "https://mempool.space/api/v1/prices";

    public CurrencyRateService(HttpClient httpClient, ILogger<CurrencyRateService> logger, ICacheStorage cacheStorage)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _cacheStorage = cacheStorage ?? throw new ArgumentNullException(nameof(cacheStorage));
    }

    public async Task<decimal> GetBtcToCurrencyRate(string currencyCode)
    {
        if (string.IsNullOrWhiteSpace(currencyCode))
            throw new ArgumentException("Currency code must not be null or empty.", nameof(currencyCode));

        var preferredCurrency = currencyCode.ToUpper();

        // Check if the rate is cached and still valid
        var cachedRate = GetCachedRate(preferredCurrency);
        if (cachedRate.HasValue)
        {
            _logger.LogInformation($"Using cached rate for {preferredCurrency}: {cachedRate.Value}");
            return cachedRate.Value;
        }

        // Fetch the latest rate from API
        var latestRate = await FetchRateFromApi(preferredCurrency);
        CacheRate(preferredCurrency, latestRate);

        return latestRate;
    }

    private decimal? GetCachedRate(string currencyCode)
    {
        var cachedEntry = _cacheStorage.GetCurrencyRate(currencyCode);
        if (cachedEntry != null && DateTime.UtcNow - cachedEntry.Timestamp < _cacheDuration)
        {
            return cachedEntry.Rate;
        }

        return null;
    }

    private async Task<decimal> FetchRateFromApi(string currencyCode)
    {
        try
        {
            var response = await _httpClient.GetAsync(ApiUrl);
            response.EnsureSuccessStatusCode();

            var jsonString = await response.Content.ReadAsStringAsync();
            _logger.LogInformation($"API Response: {jsonString}");

            var data = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(jsonString);
            if (data != null && data.TryGetValue(currencyCode, out var rateElement))
            {
                return rateElement.GetDecimal();
            }

            throw new KeyNotFoundException($"Currency '{currencyCode}' not found in the API response.");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Error while fetching data from API.");
            throw new Exception("Failed to fetch BTC rate from the API.", ex);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Error parsing the API response.");
            throw new Exception("Invalid response format from the API.", ex);
        }
    }

    private void CacheRate(string currencyCode, decimal rate)
    {
        _cacheStorage.SetCurrencyRate(currencyCode, new RateCacheEntry
        {
            Rate = rate,
            Timestamp = DateTime.UtcNow
        });
        _logger.LogInformation($"Cached new rate for {currencyCode}: {rate}");
    }
}

public class RateCacheEntry
{
    public decimal Rate { get; set; }
    public DateTime Timestamp { get; set; }
}
