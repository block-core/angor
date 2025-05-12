using Angor.Shared.Models;
using Blockcore.Consensus.TransactionInfo;
using Blockcore.NBitcoin;
using Blockcore.NBitcoin.BIP32;
using Blockcore.Networks;
using PSBT = NBitcoin.PSBT;

namespace Angor.Shared;

public interface IWalletOperations
{
    string GenerateWalletWords();
    Task<OperationResult<Transaction>> SendAmountToAddress(WalletWords walletWords, SendInfo sendInfo);
    AccountInfo BuildAccountInfoForWalletWords(WalletWords walletWords);
    AccountInfo BuildAccountInfoForExtPubKey(string extPubKey);
    Task UpdateDataForExistingAddressesAsync(AccountInfo accountInfo);
    Task UpdateAccountInfoWithNewAddressesAsync(AccountInfo accountInfo);
    Task<(string address, List<UtxoData> data)> FetchUtxoForAddressAsync(string adddress);
    List<UtxoDataWithPath> FindOutputsForTransaction(long sendAmountat, AccountInfo accountInfo);
    Task<IEnumerable<FeeEstimation>> GetFeeEstimationAsync();
    decimal CalculateTransactionFee(SendInfo sendInfo, AccountInfo accountInfo, long feeRate);
    (List<Coin>? coins, List<Key> keys) GetUnspentOutputsForTransaction(WalletWords walletWords, List<UtxoDataWithPath> utxoDataWithPaths);

    TransactionInfo AddInputsAndSignTransaction(string changeAddress, Transaction transaction,
        WalletWords walletWords, AccountInfo accountInfo, long feeRate);

    Task<OperationResult<Transaction>> PublishTransactionAsync(Network network,
        Transaction signedTransaction);
    Task<OperationResult<string>> PublishRawTransactionAsync(Network network, string trxhex);
    TransactionInfo AddFeeAndSignTransaction(string changeAddress, Transaction transaction,
        WalletWords walletWords, AccountInfo accountInfo, long feeRate);

    List<UtxoData> UpdateAccountUnconfirmedInfoWithSpentTransaction(AccountInfo accountInfo, Transaction transaction);
    //create a PSBT transaction with the given inputs and outputs, it just wont have the script sigs and wont be signed
    //this is used to create a transaction that can be signed by the user and then broadcasted to the network
    PSBT SendAmountToAddressPSBT(string extPubKey, SendInfo sendInfo);
    
}