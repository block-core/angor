using System.Globalization;
using Angor.Client.Storage;

public class CurrencyService : ICurrencyService
{
    private readonly ICurrencyRateService _currencyRateService;
    private readonly ILogger<CurrencyService> _logger;
    private readonly IClientStorage _storage;

    public CurrencyService(ICurrencyRateService currencyRateService, IClientStorage storage,
        ILogger<CurrencyService> logger)
    {
        _currencyRateService = currencyRateService;
        _storage = storage;
        _logger = logger;
    }

    public async Task<string> GetBtcValueInPreferredCurrency(decimal btcBalance)
    {
        try
        {
            var currencyCode = _storage.GetCurrencyDisplaySetting();
            var rate = await _currencyRateService.GetBtcToCurrencyRate(currencyCode);
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