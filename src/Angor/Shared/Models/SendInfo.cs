using Blockcore.NBitcoin;
using Blockcore.Consensus.TransactionInfo;

namespace Angor.Shared.Models;

public class SendInfo
{
    public string SendToAddress { get; set; }
    public long SendAmount { get; set; }
    public decimal SendFee { get; set; }
    public int FeeBlockCount { get; set; } = 1;

    public long FeeRate { get; set; } = 0;

    public string ChangeAddress { get; set; }

    public TransactionInfo? SignedTransaction { get; set; }

    public Transaction UnSignedTransaction { get; set; }

    public Dictionary<string, UtxoDataWithPath> SendUtxos { get; set; } = new();
}