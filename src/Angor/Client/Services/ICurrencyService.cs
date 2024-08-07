using System.Threading.Tasks;

public interface ICurrencyService
{
    Task<string> GetBtcValueInPreferredCurrency(decimal btcBalance);
    string GetCurrencySymbol(string currencyCode);
}