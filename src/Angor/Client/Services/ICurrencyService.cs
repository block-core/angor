public interface ICurrencyService
{
    Task<IReadOnlyList<string>> GetBtcValuesInPreferredCurrency(params decimal[] btcBalances);
    string GetCurrencySymbol(string currencyCode);
}