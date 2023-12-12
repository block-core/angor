using Angor.Shared.Models;
using Blockcore.Consensus.TransactionInfo;
using Blockcore.NBitcoin;
using Blockcore.Networks;

namespace Angor.Shared;

public interface IWalletOperations
{
    string GenerateWalletWords();
    Task<OperationResult<Transaction>> SendAmountToAddress(WalletWords walletWords, SendInfo sendInfo);
    AccountInfo BuildAccountInfoForWalletWords(WalletWords walletWords);
    Task UpdateDataForExistingAddressesAsync(AccountInfo accountInfo);
    Task UpdateAccountInfoWithNewAddressesAsync(AccountInfo accountInfo, UnconfirmedInfo unconfirmedInfo);
    Task<(string address, List<UtxoData> data)> FetchUtxoForAddressAsync(string adddress);
    List<UtxoDataWithPath> FindOutputsForTransaction(long sendAmountat, AccountInfo accountInfo, UnconfirmedInfo unconfirmedInfo);
    Task<IEnumerable<FeeEstimation>> GetFeeEstimationAsync();
    decimal CalculateTransactionFee(SendInfo sendInfo,AccountInfo accountInfo, UnconfirmedInfo unconfirmedInfo, long feeRate);
    (List<Coin>? coins, List<Key> keys) GetUnspentOutputsForTransaction(WalletWords walletWords, List<UtxoDataWithPath> utxoDataWithPaths);

    Transaction AddInputsAndSignTransaction(string changeAddress, Transaction transaction,
        WalletWords walletWords, AccountInfo accountInfo, UnconfirmedInfo unconfirmedInfo,
        FeeEstimation feeRate);

    Task<OperationResult<Transaction>> PublishTransactionAsync(Network network,
        Transaction signedTransaction);

    Transaction AddFeeAndSignTransaction(string changeAddress, Transaction transaction,
        WalletWords walletWords, AccountInfo accountInfo, UnconfirmedInfo unconfirmedInfo,
        FeeEstimation feeRate);

    void UpdateAccountInfoWithSpentTransaction(AccountInfo accountInfo, UnconfirmedInfo unconfirmedInfo, Transaction transaction);

    void AddInputsAndOutputsAsPending(UnconfirmedInfo unconfirmedInfo, Blockcore.Consensus.TransactionInfo.Transaction transaction);
    void RemoveInputsAndOutputsFromPending(UnconfirmedInfo unconfirmedInfo, string trxid);
}