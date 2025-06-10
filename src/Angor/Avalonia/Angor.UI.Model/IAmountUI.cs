namespace Angor.UI.Model;

public interface IAmountUI
{
    long Sats { get; }

    public string Symbol => "BTC";
    
    public string BtcString => $"{Btc:0.00 000 000} " + Symbol;

    private decimal Btc => Sats / (decimal)1_0000_0000;

    public string DecimalString => $"{Btc:G} {Symbol}";

    public string SatsString => $"{Sats} sats";
    public string FeeRateString => $"{Sats} sats/VByte";
    public bool IsNegative => Sats < 0;
    public bool IsPositive => Sats > 0;
    public bool IsZero => Sats == 0;
}