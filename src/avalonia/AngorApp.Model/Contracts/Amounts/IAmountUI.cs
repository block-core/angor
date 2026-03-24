namespace AngorApp.Model.Contracts.Amounts;

public interface IAmountUI
{
    long Sats { get; }

    string Symbol { get; }
    
    public string BtcString => $"{Btc:0.00 000 000} " + Symbol;

    public decimal Btc => Sats / (decimal)1_0000_0000;

    public string DecimalString => $"{Btc:G} {Symbol}";
    public string RoundedDecimalString => $"{Btc:N2} {Symbol}";
    
    public string ShortDecimalString
    {
        get
        {
            if (Btc < 0.0001m)
            {
                return SatsString;
            }

            if (DecimalString.Length > 8)
            {
                return RoundedDecimalString;
            }

            return DecimalString;
        }
    }

    public string SatsString => $"{Sats} sats";
    public string FeeRateString => $"{Sats} sats/VByte";
    public bool IsNegative => Sats < 0;
    public bool IsPositive => Sats > 0;
    public bool IsZero => Sats == 0;
}