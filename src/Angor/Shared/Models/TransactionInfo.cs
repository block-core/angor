using Blockcore.Consensus.TransactionInfo;

namespace Angor.Shared.Models;

public class TransactionInfo
{
    public Transaction Transaction { get; set; } = null!;
    public long TransactionFee { get; set; }
   
}