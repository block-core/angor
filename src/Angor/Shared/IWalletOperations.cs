using Angor.Shared.Models;
using Blockcore.Consensus.TransactionInfo;

namespace Angor.Shared;

public interface IWalletOperations
{
    string GenerateWalletWords();
    Task<OperationResult<Transaction>> SendAmountToAddress(WalletWords walletWords, SendInfo sendInfo);
    AccountInfo BuildAccountInfoForWalletWords(WalletWords walletWords);
    Task<AccountInfo> FetchDataForExistingAddressesAsync(AccountInfo accountInfo);
    Task<AccountInfo> FetchDataForNewAddressesAsync(AccountInfo accountInfo);
    Task<(string address, List<UtxoData> data)> FetchUtxoForAddressAsync(string adddress);
    Task<IEnumerable<FeeEstimation>> GetFeeEstimationAsync();
    decimal CalculateTransactionFee(SendInfo sendInfo,AccountInfo accountInfo, long feeRate);
}