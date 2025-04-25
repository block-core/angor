namespace Angor.Contexts.Wallet.Domain;

public static class BroadcastedTransactionExtensions
{
    public static Balance GetBalance(this BroadcastedTransaction tx)
    {
        long totalInputs  = tx.WalletInputs .Sum(i => i.Amount.Sats);
        long totalOutputs = tx.WalletOutputs.Sum(o => o.Amount.Sats);
        return new Balance(totalOutputs - totalInputs);
    }
    
    public static Amount GetTotalSent(this BroadcastedTransaction tx)
        => new Amount(tx.AllOutputs
            .Where(o => tx.WalletOutputs.All(w => w.Address != o.Address))
            .Sum(o => o.Amount.Sats));

    /// <summary>
    /// Total recibido (cambios) de vuelta a tu cartera.
    /// </summary>
    public static Amount GetTotalReceived(this BroadcastedTransaction tx)
        => new Amount(tx.WalletOutputs.Sum(o => o.Amount.Sats));
    
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
    
    public static IReadOnlyCollection<Address> GetRecipientAddresses(this BroadcastedTransaction tx)
        => tx.AllOutputs
            .Where(o => tx.WalletOutputs.All(w => w.Address != o.Address))
            .Select(o => o.Address)
            .Distinct()
            .ToList();
}