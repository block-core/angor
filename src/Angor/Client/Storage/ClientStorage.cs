using Angor.Client.Shared.Models;
using Blazored.LocalStorage;

namespace Angor.Client.Storage;

public class ClientStorage : IClientStorage
{
    private readonly ISyncLocalStorageService _storage;

    public ClientStorage(ISyncLocalStorageService storage)
    {
        _storage = storage;
    }

    public void SetWalletPubkey(string pubkey)
    {
        _storage.SetItemAsString("pubkey", pubkey);
    }

    public string? GetWalletPubkey()
    {
        return _storage.GetItemAsString("pubkey");
    }

    public AccountInfo GetAccountInfo(string network)
    {
        return _storage.GetItem<AccountInfo>($"utxo:{network}");
    }
        
    public void SetAccountInfo(string network, AccountInfo items)
    {
        _storage.SetItem($"utxo:{network}", items);
    }

    public void DeleteAccountInfo(string network)
    {
        _storage.RemoveItem($"utxo:{network}");
    }
}