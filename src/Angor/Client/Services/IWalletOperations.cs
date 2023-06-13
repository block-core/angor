using Angor.Client.Shared.Models;

namespace Angor.Client.Services;

public interface IWalletOperations
{
    Task<(bool, string)> SendAmountToAddress(decimal sendAmount, long selectedFee, string sendToAddress);
    void BuildAccountInfoForWalletWords();
    Task UpdateAccountInfo();
    Task<(bool noHistory, List<UtxoData> data)> FetchUtxos(string adddress);
}