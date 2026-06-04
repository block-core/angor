using Blockcore.Consensus.TransactionInfo;

namespace Angor.Shared.Models;

public class TransactionInfo
{
    public Transaction Transaction { get; set; }
    public long TransactionFee { get; set; }

    /// <summary>
    /// Network-specific transaction hex. On Bitcoin this is the same as Transaction.ToHex().
    /// On Liquid, this holds the Elements-format transaction hex.
    /// </summary>
    public string? TransactionHex { get; set; }

    /// <summary>
    /// Network-specific transaction ID. On Bitcoin this is the same as Transaction.GetHash().
    /// On Liquid, this holds the Elements transaction hash.
    /// </summary>
    public string? TransactionId { get; set; }
}