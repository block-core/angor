using NBitcoin;

public interface INBitcoinService
{
    PSBT CreatePSBT(Transaction transaction, Network network);
    PSBT AddCoins(PSBT psbt, IEnumerable<Coin> coins);
    PSBT AddKeys(PSBT psbt, IEnumerable<Key> keys);
    PSBT FinalizePSBT(PSBT psbt);
    Transaction ExtractSignedTransaction(PSBT psbt);
}