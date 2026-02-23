using Blockcore.NBitcoin;
using Blockcore.Consensus.TransactionInfo;

namespace Angor.Shared.Models;

public class SendInfo
{
    public string SendToAddress { get; set; } = string.Empty;
    public long SendAmount { get; set; }
    public decimal SendFee { get; set; }
    public int FeeBlockCount { get; set; } = 1;

    public long FeeRate { get; set; } = 0;

    public string ChangeAddress { get; set; } = string.Empty;

    public TransactionInfo? SignedTransaction { get; set; }

    public Transaction UnSignedTransaction { get; set; } = null!;

    public Dictionary<string, UtxoDataWithPath> SendUtxos { get; set; } = new();
}