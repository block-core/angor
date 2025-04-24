namespace Angor.UI.Model;

public class AmountUI(long sats)
{
    public long Sats { get; } = sats;

    public string BtcString
    {
        get
        {
            var btc = Sats / (decimal)10000_0000;
            return $"{btc:0.0000 0000}" + " BTC";
        }
    }

    public object SatsString => $"{Sats} sats";
    public object FeeRateString => $"{Sats} sats/VByte";
}