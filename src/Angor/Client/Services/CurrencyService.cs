using System;
using System.Globalization;
using System.Net.Http;
using System.Threading.Tasks;
using Angor.Client.Storage;
using System.Text.Json;
using System.Collections.Generic;

public class CurrencyService : ICurrencyService
{
    private readonly HttpClient _httpClient;
    private readonly IClientStorage _storage;
    private readonly ILogger<CurrencyService> _logger;

    public CurrencyService(HttpClient httpClient, IClientStorage storage, ILogger<CurrencyService> logger)
    {
        _httpClient = httpClient;
        _storage = storage;
        _logger = logger;
    }

    public async Task<string> GetBtcValueInPreferredCurrency(decimal btcBalance)
    {
        try
        {
            var rate = await GetBtcToPreferredCurrencyRate();
            var currencyCode = _storage.GetCurrencyDisplaySetting();
            var currencySymbol = GetCurrencySymbol(currencyCode);
            var cultureInfo = new CultureInfo(GetCultureCode(currencyCode));
            var value = btcBalance * rate;
            return value.ToString("C2", cultureInfo).Replace(cultureInfo.NumberFormat.CurrencySymbol, currencySymbol);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error getting BTC value in preferred currency: {ex.Message}");
            return "Error fetching value";
        }
    }

    private async Task<decimal> GetBtcToPreferredCurrencyRate()
    {
        using (var client = new HttpClient())
        {
            string preferredCurrency = _storage.GetCurrencyDisplaySetting().ToUpper();
            client.BaseAddress = new Uri("https://mempool.space/");
            var response = await client.GetAsync("api/v1/prices");
            if (response.IsSuccessStatusCode)
            {
                var jsonString = await response.Content.ReadAsStringAsync();
                
                _logger.LogInformation($"API Response: {jsonString}");

                var data = JsonSerializer.Deserialize<Dictionary<string, decimal>>(jsonString);

                if (data != null && data.ContainsKey(preferredCurrency))
                {
                    return data[preferredCurrency];
                }
                throw new Exception($"Currency '{preferredCurrency}' not found in the API response.");
            }
            throw new Exception("Failed to fetch BTC rate from the API.");
        }
    }

    public string GetCurrencySymbol(string currencyCode)
    {
        return currencyCode.ToUpper() switch
        {
            "USD" => "$",
            "GBP" => "£",
            "EUR" => "€",
            "CAD" => "C$",
            "CHF" => "CHF",
            "AUD" => "A$",
            "JPY" => "¥",
            _ => currencyCode
        };
    }

    private static string GetCultureCode(string currencyCode)
    {
        return currencyCode.ToUpper() switch
        {
            "USD" => "en-US",
            "GBP" => "en-GB",
            "EUR" => "fr-FR",
            "CAD" => "en-CA",
            "CHF" => "de-CH",
            "AUD" => "en-AU",
            "JPY" => "ja-JP",
            _ => "en-US"
        };
    }
}
