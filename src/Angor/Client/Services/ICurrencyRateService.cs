public interface ICurrencyRateService
{
    Task<decimal> GetBtcToCurrencyRate(string currencyCode);
}