using Angor.Client.Shared.Models;

namespace Angor.Client.Services;

public interface IWalletOperations
{
    string GenerateWalletWords();
    Task<(bool, string)> SendAmountToAddress(decimal sendAmount, long selectedFee, string sendToAddress);
    void BuildAccountInfoForWalletWords();
    Task<AccountInfo> UpdateAccountInfo();
    Task<(bool noHistory, List<UtxoData> data)> FetchUtxoForAddressAsync(string adddress);
    Task<IEnumerable<FeeEstimation>> GetFeeEstimationAsync();
}