using Angor.Shared.Models;

namespace Angor.Client.Storage;

public interface IWalletStorage
{
    bool HasWallet();
    void SaveWalletWords(Wallet wallet);
    void DeleteWallet();
    Wallet GetWallet();
    
    
    void SetFounderKeys(FounderKeyCollection founderPubKeys);
    FounderKeyCollection GetFounderKeys();
    bool NeedsRescan();
    void SetNeedsRescan(bool value);
}