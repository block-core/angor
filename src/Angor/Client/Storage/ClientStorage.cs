using Angor.Shared;
using Angor.Shared.Models;
using Blazored.LocalStorage;

namespace Angor.Client.Storage;

public class ClientStorage : IAccountStorage, INetworkStorage
{
    private readonly ISyncLocalStorageService _storage;
    
    private const string utxoKey = "utxo:{0}";
    public ClientStorage(ISyncLocalStorageService storage)
    {
        _storage = storage;
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

    

    public SettingsInfo GetSettingsInfo()
    {
        var ret = _storage.GetItem<SettingsInfo>("settings-info");

        return ret ?? new SettingsInfo();

    }

    public void SetSettingsInfo(SettingsInfo settingsInfo)
    {
        _storage.SetItem("settings-info", settingsInfo);
    }

    public SettingsInfo GetSettings()
    {
        return GetSettingsInfo();
    }

    public void SetSettings(SettingsInfo settingsInfo)
    {
        SetSettingsInfo(settingsInfo);
    }

    public void WipeStorage()
    {
        _storage.Clear();
    }
}