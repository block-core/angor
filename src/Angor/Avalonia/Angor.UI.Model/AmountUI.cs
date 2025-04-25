using Angor.Contexts.Wallet.Domain;

namespace Angor.UI.Model;

public interface IAmountUI
{
    long Sats { get; }
    public string BtcString
    {
        get
        {
            var btc = Sats / (decimal)10000_0000;
            return $"{btc:0.0000 0000}" + " BTC";
        }
    }

    public string SatsString => $"{Sats} sats";
    public string FeeRateString => $"{Sats} sats/VByte";
    public bool IsNegative => Sats < 0;
    public bool IsPositive => Sats > 0;
    public bool IsZero => Sats == 0;
}

public class AmountUI(long sats) : IAmountUI
{
    public long Sats { get; } = sats;
}