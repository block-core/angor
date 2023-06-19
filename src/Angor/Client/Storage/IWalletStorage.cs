using Angor.Client.Shared.Models;

namespace Angor.Client.Storage;

public interface IWalletStorage
{
    bool HasWallet();
    void SaveWalletWords(WalletWords walletWords);
    void DeleteWallet();
    WalletWords GetWallet();
    
    public string? GetWalletWords();
    public void DeleteWalletWords();
}