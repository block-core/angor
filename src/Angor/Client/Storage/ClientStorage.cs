using Angor.Client.Models;
using Angor.Shared;
using Angor.Shared.Models;
using Blazored.LocalStorage;
using Microsoft.Extensions.Logging;

namespace Angor.Client.Storage;

public class ClientStorage : IClientStorage, INetworkStorage
{
    private const string CurrencyDisplaySettingKey = "currencyDisplaySetting";

    private const string utxoKey = "utxo:{0}";
    private readonly ISyncLocalStorageService _storage;
    private readonly ILogger<ClientStorage> _logger;

    public ClientStorage(ISyncLocalStorageService storage, ILogger<ClientStorage> logger)
    {
        _storage = storage;
        _logger = logger;
    }

    public AccountInfo GetAccountInfo(string network)
    {
        return _storage.GetItem<AccountInfo>(string.Format(utxoKey, network));
    }

    public void SetAccountInfo(string network, AccountInfo items)
    {
        _storage.SetItem(string.Format(utxoKey, network), items);
    }

    public void DeleteAccountInfo(string network)
    {
        _storage.RemoveItem(string.Format(utxoKey, network));
    }

    public void AddInvestmentProject(InvestorProject project)
    {
        _logger.LogDebug("Attempting to add investment project with ID {ProjectId}", project.ProjectInfo.ProjectIdentifier);
        var ret = GetInvestmentProjects();

        if (ret.Any(a => a.ProjectInfo?.ProjectIdentifier == project.ProjectInfo.ProjectIdentifier)) return;

        ret.Add(project);

        _storage.SetItem("projects", ret);
        _logger.LogInformation($"Added investment project with ID {project.ProjectInfo.ProjectIdentifier}. Total projects: {ret.Count}");
    }

    public void UpdateInvestmentProject(InvestorProject project)
    {
        _logger.LogDebug("Attempting to update investment project with ID {ProjectId}", project.ProjectInfo.ProjectIdentifier);
        var ret = GetInvestmentProjects();

        var item = ret.FirstOrDefault(_ => _.ProjectInfo?.ProjectIdentifier == project.ProjectInfo.ProjectIdentifier);

        if (!ret.Remove(item))
            throw new InvalidOperationException();

        ret.Add(project);

        _storage.SetItem("projects", ret);
        _logger.LogInformation($"Updated investment project with ID {project.ProjectInfo.ProjectIdentifier}. Total projects: {ret.Count}");
    }

    public void RemoveInvestmentProject(string projectId)
    {
        _logger.LogDebug("Attempting to remove investment project with ID {ProjectId}", projectId);
        var ret = GetInvestmentProjects();

        var item = ret.FirstOrDefault(_ => _.ProjectInfo?.ProjectIdentifier == projectId);

        ret.Remove(item);

        _storage.SetItem("projects", ret);
        _logger.LogInformation($"Removed investment project with ID {projectId}. Total projects: {ret.Count}");
    }

    public List<InvestorProject> GetInvestmentProjects()
    {
        _logger.LogDebug("Fetching investment projects from storage");
        var ret = _storage.GetItem<List<InvestorProject>>("projects");
        _logger.LogInformation($"Fetched {ret?.Count ?? 0} investment projects from storage.");
        return ret ?? new List<InvestorProject>();
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

    public FounderProject? GetFounderProjects(string projectIdentifier)
    {
        var ret = _storage.GetItem<List<FounderProject>>("founder-projects");

        return ret?.FirstOrDefault(_ => _.ProjectInfo.ProjectIdentifier == projectIdentifier);
    }

    public void UpdateFounderProject(FounderProject project)
    {
        var projects = _storage.GetItem<List<FounderProject>>("founder-projects") ?? new List<FounderProject>();

        var existingProject = projects.FirstOrDefault(p => p.ProjectInfo.ProjectIdentifier == project.ProjectInfo.ProjectIdentifier);

        if (existingProject != null)
        {
            projects.Remove(existingProject);
        }

        projects.Add(project);
        
        _storage.SetItem("founder-projects", projects.OrderBy(p => p.ProjectIndex).ToList());
    }

    public void DeleteFounderProjects()
    {
        _storage.RemoveItem("founder-projects");
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

    public void WipeStorage()
    {
        _storage.Clear();
    }

    public void SetNostrPublicKeyPerProject(string projectId, string nostrPubKey)
    {
        _storage.SetItem($"project:{projectId}:nostrKey", nostrPubKey);
    }

    public string GetNostrPublicKeyPerProject(string projectId)
    {
        return _storage.GetItem<string>($"project:{projectId}:nostrKey");
    }

    public string GetCurrencyDisplaySetting()
    {
        return _storage.GetItem<string>(CurrencyDisplaySettingKey) ?? "BTC";
    }

    public void SetCurrencyDisplaySetting(string setting)
    {
        _storage.SetItem(CurrencyDisplaySettingKey, setting);
    }

    public SettingsInfo GetSettings()
    {
        return GetSettingsInfo();
    }

    public void SetSettings(SettingsInfo settingsInfo)
    {
        SetSettingsInfo(settingsInfo);
    }

    public void SetNetwork(string network)
    {
        _storage.SetItem("network", network);
    }

    public string GetNetwork()
    {
        return _storage.GetItem<string>("network");
    }

    public void DeleteInvestmentProjects()
    {
        _storage.RemoveItem("projects");
    }

    public void AddOrUpdateSignatures(SignatureInfo signatureInfo)
    {
        var ret = GetSignatures();

        var item = ret.FirstOrDefault(f => f.ProjectIdentifier == signatureInfo.ProjectIdentifier);

        if (item != null) ret.Remove(item);

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

        if (item != null) ret.Remove(item);

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

    
}