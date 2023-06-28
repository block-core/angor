using Angor.Client.Shared.Models;
using Blockcore.Consensus.TransactionInfo;

namespace Angor.Client.Services;

public interface IWalletOperations
{
    string GenerateWalletWords();
    Task<OperationResult<Transaction>> SendAmountToAddress(SendInfo sendInfo);
    void BuildAccountInfoForWalletWords();
    Task<AccountInfo> FetchDataForExistingAddressesAsync();
    Task<AccountInfo> FetchDataForNewAddressesAsync();
    Task<(string address, List<UtxoData> data)> FetchUtxoForAddressAsync(string adddress);
    Task<IEnumerable<FeeEstimation>> GetFeeEstimationAsync();
    void CalculateTransactionFee(SendInfo sendInfo, long feeRate);
    void DeleteWallet();
}