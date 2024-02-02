using Angor.Shared.Models;
using Blazored.LocalStorage;

namespace Angor.Client.Storage;

public class WalletStorage : IWalletStorage
{
    private readonly ISyncLocalStorageService _storage;

    private const string WalletWordsKey = "mnemonic";
    private const string PubKey = "pubkey";

    public WalletStorage(ISyncLocalStorageService storage)
    {
        _storage = storage;
    }

    public bool HasWallet()
    {
        return _storage.ContainKey(WalletWordsKey);
    }

    public void SaveWalletWords(WalletWords walletWords)
    {
        if (_storage.GetItem<WalletWords>(WalletWordsKey) != null)
        {
            throw new ArgumentNullException("Wallet already exists!");
        }

        _storage.SetItem(WalletWordsKey,walletWords);
    }

    public void DeleteWallet()
    {
        _storage.RemoveItem(WalletWordsKey);
    }

    public WalletWords GetWallet()
    {
        var words = _storage.GetItem<WalletWords>(WalletWordsKey);

        if (words == null)
        {
            throw new ArgumentNullException("Wallet not found!");

        }

        return words;
    }
    
    
    
    public void SetFounderKeys(FounderKeyCollection founderPubKeys)
    {
        _storage.SetItem("projectsKeys", founderPubKeys);
    }

    public FounderKeyCollection GetFounderKeys()
    {
        return _storage.GetItem<FounderKeyCollection>("projectsKeys");
    }

    public void DeleteFounderKeys()
    {
        _storage.RemoveItem("projectsKeys");
    }

    public string? GetWalletPubkey()
    {
        return _storage.GetItemAsString(PubKey);
    }

    public void DeleteWalletPubkey()
    {
        _storage.RemoveItem(PubKey);
    }
}