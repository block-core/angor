namespace Angor.Client.Services;

public interface ICurrencyRateService
{
    Task<decimal> GetBtcToCurrencyRate(string currencyCode);
}