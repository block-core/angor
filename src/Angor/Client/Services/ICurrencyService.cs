public interface ICurrencyService
{
    Task<IReadOnlyList<string>> GetBtcValuesInPreferredCurrency(params long[] btcBalances);
    string GetCurrencySymbol(string currencyCode);
    decimal ToBtc(long satoshis);
    long ToLong(decimal btcAmount);
}