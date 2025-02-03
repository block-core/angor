using Blockcore.NBitcoin;

namespace Angor.Shared.Models;

public class SendInfo
{
    public string SendToAddress { get; set; }
    public long SendAmount { get; set; }
    public decimal SendFee { get; set; }
    public int FeeBlockCount { get; set; } = 1;

    public long FeeRate { get; set; } = 0;

    public long FeeRateSat
    {
        get => FeeRate;
        set => FeeRate = value;
    }

    public string ChangeAddress { get; set; }

    public Dictionary<string, UtxoDataWithPath> SendUtxos { get; set; } = new();
}