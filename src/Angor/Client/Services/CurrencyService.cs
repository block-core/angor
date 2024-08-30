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

    /// <summary>
    ///     Gets the value of a given Bitcoin (BTC) balance in the user's preferred currency.
    /// </summary>
    /// <param name="btcBalance">The Bitcoin balance to be converted.</param>
    /// <returns>A formatted string representing the BTC value in the preferred currency.</returns>
    public async Task<string> GetBtcValueInPreferredCurrency(decimal btcBalance)
    {
        try
        {
            // Log initial state
            Console.WriteLine($"[DEBUG] Fetching BTC value for balance: {btcBalance}");

            // Get the user's preferred currency code from storage
            var currencyCode = _storage.GetCurrencyDisplaySetting();
            Console.WriteLine($"[DEBUG] Preferred currency code: {currencyCode}");

            // Fetch the conversion rate from the rate service
            var rate = await _currencyRateService.GetBtcToCurrencyRate(currencyCode);
            Console.WriteLine($"[DEBUG] Fetched conversion rate: {rate} for currency: {currencyCode}");

            // Get the symbol and culture information for formatting
            var currencySymbol = GetCurrencySymbol(currencyCode);
            var cultureInfo = new CultureInfo(GetCultureCode(currencyCode));
            Console.WriteLine($"[DEBUG] Currency symbol: {currencySymbol}, Culture: {cultureInfo.Name}");

            // Calculate the value in the preferred currency
            var value = btcBalance * rate;
            Console.WriteLine($"[DEBUG] Calculated value: {value} in currency: {currencyCode}");

            // Format the value according to the culture and replace the default currency symbol
            var formattedValue = value.ToString("C2", cultureInfo)
                .Replace(cultureInfo.NumberFormat.CurrencySymbol, currencySymbol);
            Console.WriteLine($"[DEBUG] Formatted value: {formattedValue}");

            return formattedValue;
        }
        catch (Exception ex)
        {
            // Log the error with additional information
            Console.WriteLine(
                $"[ERROR] Error getting BTC value in preferred currency for balance {btcBalance}: {ex.Message}");
            return "Error fetching value";
        }
    }


    /// <summary>
    ///     Gets the currency symbol for a given currency code.
    /// </summary>
    /// <param name="currencyCode">The currency code (e.g., USD, EUR).</param>
    /// <returns>The currency symbol corresponding to the currency code.</returns>
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