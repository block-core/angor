using Angor.Sdk.Common;

namespace Angor.Sdk.Wallet.Domain;

/// <summary>
/// Extension methods for the BroadcastedTransaction class to calculate various transaction metrics.
/// </summary>
public static class BroadcastedTransactionExtensions
{
    /// <summary>
    /// Calculates the net balance change for the wallet from this transaction.
    /// </summary>
    /// <param name="tx">The transaction to analyze.</param>
    /// <returns>The net balance change (positive for incoming funds, negative for outgoing).</returns>
    public static Balance GetBalance(this BroadcastedTransaction tx)
    {
        long totalInputs  = tx.WalletInputs .Sum(i => i.Amount.Sats);
        long totalOutputs = tx.WalletOutputs.Sum(o => o.Amount.Sats);
        return new Balance(totalOutputs - totalInputs);
    }
    
    /// <summary>
    /// Calculates the total amount sent to external addresses (non-wallet addresses).
    /// </summary>
    /// <param name="tx">The transaction to analyze.</param>
    /// <returns>The total amount sent to external addresses.</returns>
    public static Amount GetTotalSent(this BroadcastedTransaction tx)
        => new Amount(tx.AllOutputs
            .Where(o => tx.WalletOutputs.All(w => w.Address != o.Address))
            .Sum(o => o.Amount.Sats));
    
    /// <summary>
    /// Calculates the total amount received by wallet addresses in this transaction.
    /// </summary>
    /// <param name="tx">The transaction to analyze.</param>
    /// <returns>The total amount received by the wallet.</returns>
    public static Amount GetTotalReceived(this BroadcastedTransaction tx)
        => new Amount(tx.WalletOutputs.Sum(o => o.Amount.Sats));
    
    /// <summary>
    /// Gets all external addresses that sent funds in this transaction.
    /// </summary>
    /// <param name="tx">The transaction to analyze.</param>
    /// <returns>A collection of external sender addresses.</returns>
    public static IReadOnlyCollection<Address> GetSenderAddresses(
        this BroadcastedTransaction tx)
    {
        return tx.AllInputs
            .Where(inp => tx.WalletInputs
                .All(wi => wi.Address != inp.Address))
            .Select(inp => inp.Address)
            .Distinct()
            .ToList();
    }
    
    /// <summary>
    /// Gets all external addresses that received funds in this transaction.
    /// </summary>
    /// <param name="tx">The transaction to analyze.</param>
    /// <returns>A collection of external recipient addresses.</returns>
    public static IReadOnlyCollection<Address> GetRecipientAddresses(this BroadcastedTransaction tx)
        => tx.AllOutputs
            .Where(o => tx.WalletOutputs.All(w => w.Address != o.Address))
            .Select(o => o.Address)
            .Distinct()
            .ToList();
}