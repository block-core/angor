using Angor.Shared.Models;

namespace Angor.Client.Storage;

public interface IWalletStorage
{
    bool HasWallet();
    void SaveWalletWords(Wallet wallet);
    void DeleteWallet();
    Wallet GetWallet();
    bool HasExtPubKey();
    
    
    void SetFounderKeys(FounderKeyCollection founderPubKeys);
    FounderKeyCollection GetFounderKeys();
}