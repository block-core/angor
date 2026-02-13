using Angor.Shared.Models;
using Blockcore.Consensus.TransactionInfo;
using Blockcore.NBitcoin;
using Blockcore.Networks;

namespace Angor.Shared;

public interface IWalletOperations
{
    string GenerateWalletWords();
    Task<OperationResult<Transaction>> SendAmountToAddress(IWalletSigner walletSigner, SendInfo sendInfo);
    AccountInfo BuildAccountInfo(IWalletSigner walletSigner);
    Task UpdateDataForExistingAddressesAsync(AccountInfo accountInfo);
    Task UpdateAccountInfoWithNewAddressesAsync(AccountInfo accountInfo);
    Task<(string address, List<UtxoData> data)> FetchUtxoForAddressAsync(string adddress);
    List<UtxoDataWithPath> FindOutputsForTransaction(long sendAmountat, AccountInfo accountInfo);
    Task<IEnumerable<FeeEstimation>> GetFeeEstimationAsync();
    Transaction CreateSendTransaction(SendInfo sendInfo, AccountInfo accountInfo);
    (List<Coin>? coins, List<Key> keys) GetUnspentOutputsForTransaction(IWalletSigner walletSigner, List<UtxoDataWithPath> utxoDataWithPaths);

    TransactionInfo AddInputsAndSignTransaction(string changeAddress, Transaction transaction,
        IWalletSigner walletSigner, AccountInfo accountInfo, long feeRate);
    
    TransactionInfo AddInputsFromAddressAndSignTransaction(string fundingAddress, string changeAddress, 
        Transaction transaction, IWalletSigner walletSigner, AccountInfo accountInfo, long feeRate);
    
    Task<OperationResult<Transaction>> PublishTransactionAsync(Network network,
        Transaction signedTransaction);

    TransactionInfo AddFeeAndSignTransaction(string changeAddress, Transaction transaction,
        IWalletSigner walletSigner, AccountInfo accountInfo, long feeRate);

    List<UtxoData> UpdateAccountUnconfirmedInfoWithSpentTransaction(AccountInfo accountInfo, Transaction transaction);
}