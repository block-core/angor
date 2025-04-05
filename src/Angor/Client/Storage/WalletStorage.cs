using Angor.Shared.Models;
using Blazored.LocalStorage;

namespace Angor.Client.Storage;

public class WalletStorage : IWalletStorage
{
    private readonly ISyncLocalStorageService _storage;

    private const string WalletKey = "wallet";
    private const string NeedsRescanKeyPrefix = "needs_rescan_";

    public WalletStorage(ISyncLocalStorageService storage)
    {
        _storage = storage;
    }

    private string GetRescanKey(Wallet wallet)
    {
        // Use a combination of wallet-specific properties to create a unique key
        // This ensures each wallet has its own rescan flag
        return $"{NeedsRescanKeyPrefix}{wallet.GetHashCode()}";
    }

    public bool HasWallet()
    {
        return _storage.ContainKey(WalletKey);
    }

    public bool NeedsRescan()
    {
        var wallet = GetWallet();
        var rescanKey = GetRescanKey(wallet);
        return _storage.ContainKey(rescanKey) && _storage.GetItem<bool>(rescanKey);
    }

    public void SetNeedsRescan(bool value)
    {
        var wallet = GetWallet();
        var rescanKey = GetRescanKey(wallet);
        
        if (value)
        {
            _storage.SetItem(rescanKey, true);
        }
        else
        {
            _storage.RemoveItem(rescanKey);
        }
    }

    public void SaveWalletWords(Wallet wallet)
    {
        if (_storage.GetItem<Wallet>(WalletKey) != null)
        {
            throw new ArgumentNullException("Wallet already exists!");
        }

        _storage.SetItem(WalletKey, wallet);
        
        // Set rescan flag for the new wallet
        var rescanKey = GetRescanKey(wallet);
        _storage.SetItem(rescanKey, true);
    }

    public void DeleteWallet()
    {
        // Clean up rescan flag for the current wallet before deleting it
        if (HasWallet())
        {
            var wallet = GetWallet();
            var rescanKey = GetRescanKey(wallet);
            _storage.RemoveItem(rescanKey);
        }
        
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