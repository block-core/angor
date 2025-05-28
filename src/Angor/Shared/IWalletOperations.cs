using Angor.Shared.Models;
using Blockcore.Consensus.TransactionInfo;
using Blockcore.NBitcoin;
using Blockcore.Networks;

namespace Angor.Shared;

public interface IWalletOperations
{
    string GenerateWalletWords();

    PsbtData CreatePsbtForTransaction(Transaction transaction, AccountInfo accountInfo, long feeRate, string? changeAddress = null, bool spendAll = false);
    TransactionInfo SignPsbt(PsbtData psbtData, WalletWords walletWords);

    Task<OperationResult<Transaction>> SendAmountToAddress(WalletWords walletWords, SendInfo sendInfo);
    AccountInfo BuildAccountInfoForWalletWords(WalletWords walletWords);
    Task UpdateDataForExistingAddressesAsync(AccountInfo accountInfo);
    Task UpdateAccountInfoWithNewAddressesAsync(AccountInfo accountInfo);
    Task<(string address, List<UtxoData> data)> FetchUtxoForAddressAsync(string adddress);
    List<UtxoDataWithPath> FindOutputsForTransaction(long sendAmountat, AccountInfo accountInfo, bool spendAll = false);
    Task<IEnumerable<FeeEstimation>> GetFeeEstimationAsync();
    decimal CalculateTransactionFee(SendInfo sendInfo, AccountInfo accountInfo, long feeRate);
    (List<Coin>? coins, List<Key> keys) GetUnspentOutputsForTransaction(WalletWords walletWords, List<UtxoDataWithPath> utxoDataWithPaths);

    TransactionInfo AddInputsAndSignTransaction(string changeAddress, Transaction transaction,
        WalletWords walletWords, AccountInfo accountInfo, long feeRate, bool spendAll = false);

    Task<OperationResult<Transaction>> PublishTransactionAsync(Network network,
        Transaction signedTransaction);

    TransactionInfo AddFeeAndSignTransaction(string changeAddress, Transaction transaction,
        WalletWords walletWords, AccountInfo accountInfo, long feeRate);

    List<UtxoData> UpdateAccountUnconfirmedInfoWithSpentTransaction(AccountInfo accountInfo, Transaction transaction);
}