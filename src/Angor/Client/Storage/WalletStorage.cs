using Angor.Client.Shared.Models;
using Blazored.LocalStorage;

namespace Angor.Client.Storage;

public class WalletStorage : IWalletStorage
{
    private readonly ISyncLocalStorageService _storage;

    private const string WalletWordsKey = "mnemonic";

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
    
    public string? GetWalletWords()
    {
        return _storage.GetItem<WalletWords>(WalletWordsKey)?
            .ConvertToString() ?? null;
    }

    public void DeleteWalletWords()
    {
        _storage.RemoveItem(WalletWordsKey);
    }
}