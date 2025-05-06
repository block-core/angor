namespace Angor.UI.Model;

public interface IAmountUI
{
    long Sats { get; }
    public string BtcString
    {
        get
        {
            var btc = Sats / (decimal)1_0000_0000;
            return $"{btc:0.00 000 000}" + " BTC";
        }
    }

    public string SatsString => $"{Sats} sats";
    public string FeeRateString => $"{Sats} sats/VByte";
    public bool IsNegative => Sats < 0;
    public bool IsPositive => Sats > 0;
    public bool IsZero => Sats == 0;
}