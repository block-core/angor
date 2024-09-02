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

    public async Task<IReadOnlyList<string>> GetBtcValuesInPreferredCurrency(params decimal[] btcBalances)
    {
        if (btcBalances == null || btcBalances.Length == 0)
        {
            _logger.LogWarning("No BTC balances provided.");
            return new List<string> { "No balances provided" };
        }

        string currencyCode;
        decimal rate;
        string currencySymbol;
        CultureInfo cultureInfo;

        try
        {
            currencyCode = _storage.GetCurrencyDisplaySetting();
            if (currencyCode.Equals("btc", StringComparison.OrdinalIgnoreCase))
                return btcBalances.Select(_ => string.Empty)
                    .ToList(); // Return a list of empty strings with the same count as btcBalances
            rate = await _currencyRateService.GetBtcToCurrencyRate(currencyCode);
            currencySymbol = GetCurrencySymbol(currencyCode);
            cultureInfo = new CultureInfo(GetCultureCode(currencyCode));
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error fetching currency settings or rates: {ex.Message}");
            return new List<string> { "Error fetching settings or rates" };
        }

        try
        {
            return btcBalances
                .AsParallel()
                .Select(btcBalance => CalculateFormattedValue(btcBalance, rate, currencySymbol, cultureInfo))
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error calculating BTC values: {ex.Message}");
            return new List<string> { "Error calculating values" };
        }
    }

    private string CalculateFormattedValue(decimal btcBalance, decimal rate, string currencySymbol,
        CultureInfo cultureInfo)
    {
        var value = btcBalance * rate;
        return value.ToString("C2", cultureInfo).Replace(cultureInfo.NumberFormat.CurrencySymbol, currencySymbol);
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