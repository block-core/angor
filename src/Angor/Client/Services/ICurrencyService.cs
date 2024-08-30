public interface ICurrencyService
{
    /// <summary>
    ///     Gets the value of a given Bitcoin (BTC) balance in the user's preferred currency.
    /// </summary>
    /// <param name="btcBalance">The Bitcoin balance to be converted.</param>
    /// <returns>A formatted string representing the BTC value in the preferred currency.</returns>
    Task<string> GetBtcValueInPreferredCurrency(decimal btcBalance);

    /// <summary>
    ///     Gets the currency symbol for a given currency code.
    /// </summary>
    /// <param name="currencyCode">The currency code (e.g., USD, EUR).</param>
    /// <returns>The currency symbol corresponding to the currency code.</returns>
    string GetCurrencySymbol(string currencyCode);
}