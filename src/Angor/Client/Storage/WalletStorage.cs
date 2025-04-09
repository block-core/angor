using Angor.Shared.Models;
using Blazored.LocalStorage;

namespace Angor.Client.Storage;

public class WalletStorage : IWalletStorage
{
    private readonly ISyncLocalStorageService _storage;

    private const string WalletKey = "wallet";

    public WalletStorage(ISyncLocalStorageService storage)
    {
        _storage = storage;
    }

    public bool HasWallet()
    {
        return _storage.ContainKey(WalletKey);
    }
    public bool HasExtPubKey()
    {
        var wallet = _storage.GetItem<Wallet>(WalletKey);
        return wallet != null && wallet.EncryptedData == "flag";
    }

    public void SaveWalletWords(Wallet wallet)
    {
        if (_storage.GetItem<Wallet>(WalletKey) != null)
        {
            throw new ArgumentNullException("Wallet already exists!");
        }

        _storage.SetItem(WalletKey, wallet);
    }

    public void DeleteWallet()
    {
        _storage.RemoveItem(WalletKey);
    }

    public Wallet GetWallet()
    {
        var wallet = _storage.GetItem<Wallet>(WalletKey);

        if (wallet == null)
        {
            throw new ArgumentNullException("Wallet not found!");
        }

        return wallet;
    }
    
    public void SetFounderKeys(FounderKeyCollection founderPubKeys)
    {
        var wallet = GetWallet();
        wallet.FounderKeys = founderPubKeys;

        _storage.SetItem(WalletKey, wallet);
    }

    public FounderKeyCollection GetFounderKeys()
    {
        return GetWallet().FounderKeys;
    }
}