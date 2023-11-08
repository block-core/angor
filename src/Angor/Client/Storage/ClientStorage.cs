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

    public void AddProject(ProjectInfo project)
    {
        var ret = GetProjects();

        ret.Add(project);

        _storage.SetItem("projects", ret);
    }

    public List<ProjectInfo> GetProjects()
    {
        var ret =  _storage.GetItem<List<ProjectInfo>>("projects");

        return ret ?? new List<ProjectInfo>();
    }

    public void AddFounderProject(ProjectInfo project)
    {
        var ret = GetProjects();

        ret.Add(project);

        _storage.SetItem("founder-projects", ret);
    }

    public List<ProjectInfo> GetFounderProjects()
    {
        var ret = _storage.GetItem<List<ProjectInfo>>("founder-projects");

        return ret ?? new List<ProjectInfo>();
    }


    public void AddBrowseProject(ProjectInfo project)
    {
        var ret = GetBrowseProjects();

        if (ret.FirstOrDefault(f => f.ProjectIdentifier == project.ProjectIdentifier) == null)
        {
            ret.Add(project);
        }

        _storage.SetItem("browse-projects", ret);
    }

    public List<ProjectInfo> GetBrowseProjects()
    {
        var ret = _storage.GetItem<List<ProjectInfo>>("browse-projects");

        return ret ?? new List<ProjectInfo>();
    }

    public void AddOrUpdateSignatures(SignatureInfo signatureInfo)
    {
        var ret = GetSignaturess();

        var item = ret.FirstOrDefault(f => f.ProjectIdentifier == signatureInfo.ProjectIdentifier);

        if (item != null)
        {
            ret.Remove(item);
        }

        ret.Add(signatureInfo);

        _storage.SetItem("recovery-signatures", ret);
    }

    public List<SignatureInfo> GetSignaturess()
    {
        var ret = _storage.GetItem<List<SignatureInfo>>("recovery-signatures");

        return ret ?? new List<SignatureInfo>();
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
}