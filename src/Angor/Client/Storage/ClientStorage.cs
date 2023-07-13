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

    public void AddProjectInfo(ProjectInfo project)
    {
        var ret = GetProjectsInfo();

        ret.Add(project);

        _storage.SetItem("projects", ret);
    }

    public List<ProjectInfo> GetProjectsInfo()
    {
        var ret =  _storage.GetItem<List<ProjectInfo>>("projects");

        if (ret == null)
        {
            ret = new List<ProjectInfo>();
            _storage.SetItem("projects",ret);
        }

        return ret;
    }

    public void SetFounderProjectInfo(ProjectInfo project)
    {
        _storage.SetItem("my-project", project);
    }

    public ProjectInfo? GetFounderProjectsInfo()
    {
        var ret = _storage.GetItem<ProjectInfo>("my-project");

        return ret;
    }
}