using Angor.Client.Models;
using Angor.Shared;
using Angor.Shared.Models;
using Blazored.LocalStorage;
using Blazored.SessionStorage;

namespace Angor.Client.Storage;

public class ClientStorage : IClientStorage, INetworkStorage
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

    public void AddInvestmentProject(InvestorProject project)
    {
        var ret = GetInvestmentProjects();

        if (ret.Any(a => a.ProjectInfo?.ProjectIdentifier == project.ProjectInfo.ProjectIdentifier))
        {
            return;
        }

        ret.Add(project);

        _storage.SetItem("projects", ret);
    }

    public void UpdateInvestmentProject(InvestorProject project)
    {
        var ret = GetInvestmentProjects();

        var item = ret.First(_ => _.ProjectInfo?.ProjectIdentifier == project.ProjectInfo.ProjectIdentifier);

        if(!ret.Remove(item)) 
            throw new InvalidOperationException();

        ret.Add(project);

        _storage.SetItem("projects", ret);
    }

    public void RemoveInvestmentProject(string projectId)
    {
        var ret = GetInvestmentProjects();

        var item = ret.First(_ => _.ProjectInfo?.ProjectIdentifier == projectId);

        ret.Remove(item);

        _storage.SetItem("projects", ret);
    }

    public void DeleteInvestmentProjects()
    {
        _storage.RemoveItem("projects");
    }

    public List<InvestorProject> GetInvestmentProjects()
    {
        var ret =  _storage.GetItem<List<InvestorProject>>("projects");

        return ret ?? new List<InvestorProject>();
    }

    public void AddInvestmentProjectMetadata(string pubkey, ProjectMetadata projectMetadata)
    {
        var ret = GetInvestmentProjectsMetadata();

        ret.TryAdd(pubkey, projectMetadata);

        _storage.SetItem("projects-metadata", ret);

    }
    public Dictionary<string, ProjectMetadata> GetInvestmentProjectsMetadata()
    {
        var ret = _storage.GetItem<Dictionary<string,ProjectMetadata>>("projects-metadata");
     
        return ret ?? new Dictionary<string, ProjectMetadata>();
    }

    public void AddFounderProject(params FounderProject[] projects)
    {
        var ret = GetFounderProjects();

        ret.AddRange(projects);

        _storage.SetItem("founder-projects", ret.OrderBy(_ => _.ProjectIndex));
    }

    public List<FounderProject> GetFounderProjects()
    {
        var ret = _storage.GetItem<List<FounderProject>>("founder-projects");

        return ret ?? new List<FounderProject>();
    }

    public void UpdateFounderProject(FounderProject project)
    {
        var projects = _storage.GetItem<List<FounderProject>>("founder-projects");
        
        var item = projects.FirstOrDefault(f => f.ProjectInfo.ProjectIdentifier == project.ProjectInfo.ProjectIdentifier);

        if (item != null)
        {
            projects.Remove(item);   
        }
        
        projects.Add(project);
        
        _storage.SetItem("founder-projects", projects.OrderBy(_ => _.ProjectIndex));
    }

    public void DeleteFounderProjects()
    {
        _storage.RemoveItem("founder-projects");
    }

    public void AddOrUpdateSignatures(SignatureInfo signatureInfo)
    {
        var ret = GetSignatures();

        var item = ret.FirstOrDefault(f => f.ProjectIdentifier == signatureInfo.ProjectIdentifier);

        if (item != null)
        {
            ret.Remove(item);
        }

        ret.Add(signatureInfo);

        _storage.SetItem("recovery-signatures", ret);
    }

    public List<SignatureInfo> GetSignatures()
    {
        var ret = _storage.GetItem<List<SignatureInfo>>("recovery-signatures");

        return ret ?? new List<SignatureInfo>();
    }

    public void RemoveSignatures(SignatureInfo signatureInfo)
    {
        var ret = GetSignatures();

        var item = ret.FirstOrDefault(f => f.ProjectIdentifier == signatureInfo.ProjectIdentifier);

        if (item != null)
        {
            ret.Remove(item);
        }

        _storage.SetItem("recovery-signatures", ret);
    }

    public void DeleteSignatures()
    {
        // signatures are valuable to have so to avoid losing them forever 
        // we just store them in new entry we will lever use again.
        var sigs = GetSignatures();
        _storage.SetItem($"recovery-signatures-{DateTime.UtcNow.Ticks}", sigs);

        _storage.RemoveItem("recovery-signatures");
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