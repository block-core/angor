using NBitcoin;

public class NBitcoinService : INBitcoinService
{
    public PSBT CreatePSBT(Transaction transaction, Network network)
    {
        return PSBT.FromTransaction(transaction, network);
    }

    public PSBT AddCoins(PSBT psbt, IEnumerable<Coin> coins)
    {
        foreach (var coin in coins)
        {
            psbt.AddCoins(coin);
        }
        return psbt;
    }

    public PSBT AddKeys(PSBT psbt, IEnumerable<Key> keys)
    {
        return psbt.SignWithKeys(keys.ToArray());
    }

    public PSBT FinalizePSBT(PSBT psbt)
    {
        psbt.Finalize();
        return psbt;
    }

    public Transaction ExtractSignedTransaction(PSBT psbt)
    {
        return psbt.ExtractTransaction();
    }
}