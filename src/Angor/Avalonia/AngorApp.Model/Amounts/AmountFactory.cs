using Angor.Shared;
using AngorApp.Model.Contracts.Amounts;

namespace AngorApp.Model.Amounts;

public class AmountFactory : IAmountFactory
{
    private readonly INetworkConfiguration _networkConfiguration;
    
    public AmountFactory(INetworkConfiguration networkConfiguration)
    {
        _networkConfiguration = networkConfiguration;
    }
    
    public string CurrencySymbol => _networkConfiguration.GetNetwork().CoinTicker;
    
    public IAmountUI Create(long sats)
    {
        return new AmountUI(sats, CurrencySymbol);
    }
}
