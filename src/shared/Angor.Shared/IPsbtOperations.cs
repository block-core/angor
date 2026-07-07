using Angor.Shared.Models;
using NBitcoin;

namespace Angor.Shared;

public interface IPsbtOperations
{
    PsbtData CreatePsbtForTransaction(Transaction transaction, AccountInfo accountInfo, long feeRate, string? changeAddress = null, List<UtxoDataWithPath>? utxoDataWithPaths = null);
    TransactionInfo SignPsbt(PsbtData psbtData, WalletWords walletWords);
}
