public interface ICurrencyService
{
    Task<IReadOnlyList<string>> GetBtcValuesInPreferredCurrency(params long[] satBalances);
    string GetCurrencySymbol(string currencyCode);
    decimal ToBtc(long satoshis);
    long ToSatoshi(decimal btcAmount);
}