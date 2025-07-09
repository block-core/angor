using Angor.Shared.Models;
using Blockcore.Consensus.TransactionInfo;
using Blockcore.NBitcoin;
using Blockcore.Networks;

namespace Angor.Shared;

public interface IPsbtOperations
{
    PsbtData CreatePsbtForTransaction(Transaction transaction, AccountInfo accountInfo, long feeRate, string? changeAddress = null, List<UtxoDataWithPath>? utxoDataWithPaths = null);
    TransactionInfo SignPsbt(PsbtData psbtData, WalletWords walletWords);
}