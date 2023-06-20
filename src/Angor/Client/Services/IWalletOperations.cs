using Angor.Client.Shared.Models;
using Blockcore.Consensus.TransactionInfo;
using Blockcore.NBitcoin;

namespace Angor.Client.Services;

public interface IWalletOperations
{
    string GenerateWalletWords();
    Task<OperationResult<Transaction>> SendAmountToAddress(SendInfo sendInfo);
    void BuildAccountInfoForWalletWords();
    Task<AccountInfo> UpdateAccountInfo();
    Task<(bool noHistory, List<UtxoData> data)> FetchUtxoForAddressAsync(string adddress);
    Task<IEnumerable<FeeEstimation>> GetFeeEstimationAsync();

    void CalculateTransactionFee(SendInfo sendInfo, long feeRate);
}