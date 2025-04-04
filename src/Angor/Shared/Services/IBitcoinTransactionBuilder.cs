using Blockcore.Consensus.TransactionInfo;
using Blockcore.NBitcoin;
using Blockcore.Networks;

namespace Angor.Shared.Services;

public interface IBitcoinTransactionBuilder
{
    IBitcoinTransactionBuilder CreateTransactionBuilder(Network network);
    public IBitcoinTransactionBuilder Send<TDestination>(TDestination destination, Money amount);
    public IBitcoinTransactionBuilder AddCoins<T>(IEnumerable<T> coins);
    public IBitcoinTransactionBuilder SetChange<T>(T change);
    public IBitcoinTransactionBuilder SendEstimatedFees<T>(T feeRate);
    public Money EstimateFees<T>(T feeRate);
    public Transaction BuildTransaction(bool sign = true);
    public bool Verify<T, TError>(T trx, out TError[] errors);
    IBitcoinTransactionBuilder AddKeys<T>(T[] keys);
    IBitcoinTransactionBuilder ContinueToBuild<T>(T transaction);
    IBitcoinTransactionBuilder CoverTheRest();

    IBitcoinTransactionBuilder SendFees(Money minimumFee);
}