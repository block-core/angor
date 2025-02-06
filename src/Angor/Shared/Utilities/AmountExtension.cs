using System.Text.Json;
using System.Text.Json.Serialization;
using Blockcore.NBitcoin;

namespace Angor.Shared.Utilities;

public static class AmountExtension 
{
    public static decimal ToUnitBtc(this long amount)
    {
        return Money.Satoshis(amount).ToUnit(MoneyUnit.BTC);
    }
}
