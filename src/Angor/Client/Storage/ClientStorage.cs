using Angor.Shared.Models;
using Blazored.LocalStorage;

namespace Angor.Client.Storage;

public class ClientStorage : IClientStorage
{
    private readonly ISyncLocalStorageService _storage;

    private const string PubKey = "pubkey";
    private const string utxoKey = "utxo:{0}";
    public ClientStorage(ISyncLocalStorageService storage)
    {
        _storage = storage;
    }

    public void SetWalletPubkey(string pubkey)
    {
        _storage.SetItemAsString(PubKey, pubkey);
    }

    public string? GetWalletPubkey()
    {
        return _storage.GetItemAsString(PubKey);
    }

    public void DeleteWalletPubkey()
    {
        _storage.RemoveItem(PubKey);
    }

    public AccountInfo GetAccountInfo(string network)
    {
        return _storage.GetItem<AccountInfo>(string.Format(utxoKey,network));
    }
        
    public void SetAccountInfo(string network, AccountInfo items)
    {
        _storage.SetItem(string.Format(utxoKey,network), items);
    }

    public void DeleteAccountInfo(string network)
    {
        _storage.RemoveItem(string.Format(utxoKey,network));
    }
}