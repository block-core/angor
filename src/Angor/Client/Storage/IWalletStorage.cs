using Angor.Shared.Models;

namespace Angor.Client.Storage;

public interface IWalletStorage
{
    bool HasWallet();
    void SaveWalletWords(WalletWords walletWords);
    void DeleteWallet();
    WalletWords GetWallet();
    
    
    void SetFounderKeys(FounderKeyCollection founderPubKeys);
    FounderKeyCollection GetFounderKeys();
    void DeleteFounderKeys();
    string? GetWalletPubkey();
    void DeleteWalletPubkey();
}