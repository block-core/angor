using Angor.Shared;
using Angor.UI.Model;

namespace Angor.UI.Model.Implementation;

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
