using System.Globalization;
using Angor.Client.Storage;
using Angor.Shared;
using Blockcore.NBitcoin;

namespace Angor.Client.Services;

public class CurrencyService : ICurrencyService
{
    private readonly ICurrencyRateService _currencyRateService;
    private readonly ILogger<CurrencyService> _logger;
    private readonly INetworkConfiguration _networkConfiguration;
    private readonly IClientStorage _storage;

    public CurrencyService(
        ICurrencyRateService currencyRateService,
        IClientStorage storage,
        ILogger<CurrencyService> logger,
        INetworkConfiguration network)
    {
        _currencyRateService = currencyRateService ?? throw new ArgumentNullException(nameof(currencyRateService));
        _storage = storage ?? throw new ArgumentNullException(nameof(storage));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _networkConfiguration = network ?? throw new ArgumentNullException(nameof(network));
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
            _ => currencyCode.ToUpper() // Return code if no symbol found
        };
    }

    public async Task<IReadOnlyList<string>> GetBtcValuesInPreferredCurrency(params long[] satBalances)
    {
        if (satBalances == null || satBalances.Length == 0)
        {
            _logger.LogWarning("No BTC balances provided.");
            return new List<string> { "No balances provided" };
        }

        // Check if the current network is Bitcoin (BTC)
        if (!_networkConfiguration.GetNetwork().CoinTicker.Equals("BTC", StringComparison.OrdinalIgnoreCase))
        {
            return GenerateEmptyStringList(satBalances.Length);
        }

        string currencyCode;
        decimal rate;
        string currencySymbol;
        CultureInfo cultureInfo;

        try
        {
            currencyCode = _storage.GetCurrencyDisplaySetting();

            // If preferred currency is BTC, return empty strings
            if (currencyCode.Equals("BTC", StringComparison.OrdinalIgnoreCase))
            {
                return GenerateEmptyStringList(satBalances.Length);
            }

            // Get exchange rate, symbol, and culture info
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
            return satBalances
                .AsParallel()
                .Select(satsBalance => CalculateFormattedValue(satsBalance, rate, currencySymbol, cultureInfo))
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error calculating BTC values: {ex.Message}");
            return new List<string> { "Error calculating values" };
        }
    }

    private string CalculateFormattedValue(long satsBalance, decimal rate, string currencySymbol, CultureInfo cultureInfo)
    {
        var btcBalance = ToBtc(satsBalance); // Use ToBtc extension method
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
            _ => "en-US" // Default to US culture if no match found
        };
    }

    private static List<string> GenerateEmptyStringList(int length)
    {
        return Enumerable.Repeat(string.Empty, length).ToList();
    }

    // Add ToBtc and ToSatoshi methods
    public decimal ToBtc(long satoshis)
    {
        return Money.Satoshis(satoshis).ToUnit(MoneyUnit.BTC); // Convert satoshis to BTC
    }

    public long ToSatoshi(decimal btcAmount)
    {
        return Money.Coins(btcAmount).Satoshi; // Convert BTC to satoshis
    }
}