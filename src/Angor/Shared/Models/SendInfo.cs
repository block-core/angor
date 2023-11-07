using Blockcore.NBitcoin;

namespace Angor.Shared.Models;

public class SendInfo
{
    public string SendToAddress { get; set; }
    public decimal SendAmount { get; set; }
    public decimal SendFee { get; set; }
    public long SendAmountSat => Money.Coins(SendAmount).Satoshi;
    public long SendFeeSat
    {
        get => Money.Coins(SendFee).Satoshi;
        set => SendFee = Money.Satoshis(value).ToUnit(MoneyUnit.BTC);
    }
    public int FeeBlockCount { get; set; } = 1;

    public decimal FeeRate { get; set; } = 0;

    public long FeeRateSat
    {
        get => Money.Coins(FeeRate).Satoshi;
        set => FeeRate = Money.Satoshis(value).ToUnit(MoneyUnit.BTC);
    }

    public string ChangeAddress { get; set; }

    public Dictionary<string, UtxoDataWithPath> SendUtxos { get; set; } = new();
}