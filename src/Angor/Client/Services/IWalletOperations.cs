using Angor.Client.Shared.Models;

namespace Angor.Client.Services;

public interface IWalletOperations
{
    bool HasWallet();
    void CreateWallet(WalletWords walletWords);
    void DeleteWallet();
    WalletWords GetWallet();

    string GenerateWalletWords();

    Task<(bool, string)> SendAmountToAddress(decimal sendAmount, long selectedFee, string sendToAddress);
    void BuildAccountInfoForWalletWords();
    Task<AccountInfo> UpdateAccountInfo();
    Task<(bool noHistory, List<UtxoData> data)> FetchUtxos(string adddress);
}