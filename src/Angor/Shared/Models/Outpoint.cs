using Blockcore.Consensus.TransactionInfo;
using Blockcore.NBitcoin;

namespace Angor.Shared.Models;

public class Outpoint
{
    public string transactionId { get; set; }
    public int outputIndex { get; set; }

    public override string ToString()
    {
        return $"{transactionId}-{outputIndex}";
    }

    public OutPoint ToOutPoint()
    {
        return new OutPoint(uint256.Parse(transactionId), outputIndex);
    }
}