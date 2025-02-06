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
        _logger.LogDebug("Fetching account info for network {Network}", network);
        return _storage.GetItem<AccountInfo>(string.Format(utxoKey, network));
    }

    public void SetAccountInfo(string network, AccountInfo items)
    {
        _logger.LogDebug("Setting account info for network {Network}", network);
        _storage.SetItem(string.Format(utxoKey, network), items);
    }

    public void DeleteAccountInfo(string network)
    {
        _logger.LogDebug("Deleting account info for network {Network}", network);
        _storage.RemoveItem(string.Format(utxoKey, network));
    }

    public void AddInvestmentProject(InvestorProject project)
    {
        _logger.LogDebug("Attempting to add investment project with ID {ProjectId}", project.ProjectInfo.ProjectIdentifier);
        var ret = GetInvestmentProjects();

        if (ret.Any(a => a.ProjectInfo?.ProjectIdentifier == project.ProjectInfo.ProjectIdentifier))
        {
            _logger.LogWarning("Investment project with ID {ProjectId} already exists", project.ProjectInfo.ProjectIdentifier);
            return;
        }

        ret.Add(project);

        _storage.SetItem("projects", ret);
        _logger.LogInformation($"Added investment project with ID {project.ProjectInfo.ProjectIdentifier}. Total projects: {ret.Count}");
    }

    public void UpdateInvestmentProject(InvestorProject project)
    {
        _logger.LogDebug("Attempting to update investment project with ID {ProjectId}", project.ProjectInfo.ProjectIdentifier);
        var ret = GetInvestmentProjects();

        var item = ret.FirstOrDefault(_ => _.ProjectInfo?.ProjectIdentifier == project.ProjectInfo.ProjectIdentifier);

        if (item == null)
        {
            _logger.LogWarning("Investment project with ID {ProjectId} not found for update", project.ProjectInfo.ProjectIdentifier);
            return;
        }

        ret.Remove(item);
        ret.Add(project);

        _storage.SetItem("projects", ret);
        _logger.LogInformation($"Updated investment project with ID {project.ProjectInfo.ProjectIdentifier}. Total projects: {ret.Count}");
    }

    public void RemoveInvestmentProject(string projectId)
    {
        _logger.LogDebug("Attempting to remove investment project with ID {ProjectId}", projectId);
        var ret = GetInvestmentProjects();

        var item = ret.FirstOrDefault(_ => _.ProjectInfo?.ProjectIdentifier == projectId);

        if (item == null)
        {
            _logger.LogWarning("Investment project with ID {ProjectId} not found for removal", projectId);
            return;
        }

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
        _logger.LogDebug("Attempting to add founder projects");
        var ret = GetFounderProjects();

        ret.AddRange(projects);

        _storage.SetItem("founder-projects", ret.OrderBy(_ => _.ProjectIndex));
        _logger.LogInformation($"Added founder projects. Total projects: {ret.Count()}");
    }

    public List<FounderProject> GetFounderProjects()
    {
        _logger.LogDebug("Fetching founder projects from storage");
        var ret = _storage.GetItem<List<FounderProject>>("founder-projects");
        _logger.LogInformation($"Fetched {ret?.Count ?? 0} founder projects from storage.");
        return ret ?? new List<FounderProject>();
    }

    public FounderProject? GetFounderProjects(string projectIdentifier)
    {
        _logger.LogDebug("Fetching founder project with ID {ProjectId}", projectIdentifier);
        var ret = _storage.GetItem<List<FounderProject>>("founder-projects");
        return ret?.FirstOrDefault(_ => _.ProjectInfo.ProjectIdentifier == projectIdentifier);
    }

    public void UpdateFounderProject(FounderProject project)
    {
        _logger.LogDebug("Attempting to update founder project with ID {ProjectId}", project.ProjectInfo.ProjectIdentifier);
        var projects = _storage.GetItem<List<FounderProject>>("founder-projects") ?? new List<FounderProject>();

        var existingProject = projects.FirstOrDefault(p => p.ProjectInfo.ProjectIdentifier == project.ProjectInfo.ProjectIdentifier);

        if (existingProject != null)
        {
            projects.Remove(existingProject);
        }

        projects.Add(project);

        _storage.SetItem("founder-projects", projects.OrderBy(p => p.ProjectIndex).ToList());
        _logger.LogInformation($"Updated founder project with ID {project.ProjectInfo.ProjectIdentifier}. Total projects: {projects.Count}");
    }

    public void DeleteFounderProjects()
    {
        _logger.LogDebug("Deleting all founder projects from storage");
        _storage.RemoveItem("founder-projects");
    }

    public SettingsInfo GetSettingsInfo()
    {
        _logger.LogDebug("Fetching settings info from storage");
        var ret = _storage.GetItem<SettingsInfo>("settings-info");
        _logger.LogInformation($"Fetched settings info from storage.");
        return ret ?? new SettingsInfo();
    }

    public void SetSettingsInfo(SettingsInfo settingsInfo)
    {
        _logger.LogDebug("Setting settings info in storage");
        _storage.SetItem("settings-info", settingsInfo);
    }

    public void WipeStorage()
    {
        _logger.LogDebug("Wiping all storage");
        _storage.Clear();
    }

    public void SetNostrPublicKeyPerProject(string projectId, string nostrPubKey)
    {
        _logger.LogDebug("Setting Nostr public key for project ID {ProjectId}", projectId);
        _storage.SetItem($"project:{projectId}:nostrKey", nostrPubKey);
    }

    public string GetNostrPublicKeyPerProject(string projectId)
    {
        _logger.LogDebug("Fetching Nostr public key for project ID {ProjectId}", projectId);
        return _storage.GetItem<string>($"project:{projectId}:nostrKey");
    }

    public string GetCurrencyDisplaySetting()
    {
        _logger.LogDebug("Fetching currency display setting from storage");
        return _storage.GetItem<string>(CurrencyDisplaySettingKey) ?? "BTC";
    }

    public void SetCurrencyDisplaySetting(string setting)
    {
        _logger.LogDebug("Setting currency display setting in storage");
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
        _logger.LogDebug("Setting network in storage");
        _storage.SetItem("network", network);
    }

    public string GetNetwork()
    {
        _logger.LogDebug("Fetching network from storage");
        return _storage.GetItem<string>("network");
    }

    public void DeleteInvestmentProjects()
    {
        _logger.LogDebug("Deleting all investment projects from storage");
        _storage.RemoveItem("projects");
    }

    public void AddOrUpdateSignatures(SignatureInfo signatureInfo)
    {
        _logger.LogDebug("Attempting to add or update signatures for project ID {ProjectId}", signatureInfo.ProjectIdentifier);
        var ret = GetSignatures();

        var item = ret.FirstOrDefault(f => f.ProjectIdentifier == signatureInfo.ProjectIdentifier);

        if (item != null) ret.Remove(item);

        ret.Add(signatureInfo);

        _storage.SetItem("recovery-signatures", ret);
        _logger.LogInformation($"Added or updated signatures for project ID {signatureInfo.ProjectIdentifier}. Total signatures: {ret.Count}");
    }

    public List<SignatureInfo> GetSignatures()
    {
        _logger.LogDebug("Fetching signatures from storage");
        var ret = _storage.GetItem<List<SignatureInfo>>("recovery-signatures");
        _logger.LogInformation($"Fetched {ret?.Count ?? 0} signatures from storage.");
        return ret ?? new List<SignatureInfo>();
    }

    public void RemoveSignatures(SignatureInfo signatureInfo)
    {
        _logger.LogDebug("Attempting to remove signatures for project ID {ProjectId}", signatureInfo.ProjectIdentifier);
        var ret = GetSignatures();

        var item = ret.FirstOrDefault(f => f.ProjectIdentifier == signatureInfo.ProjectIdentifier);

        if (item != null) ret.Remove(item);

        _storage.SetItem("recovery-signatures", ret);
        _logger.LogInformation($"Removed signatures for project ID {signatureInfo.ProjectIdentifier}. Total signatures: {ret.Count}");
    }

    public void DeleteSignatures()
    {
        _logger.LogDebug("Deleting all signatures from storage");
        var sigs = GetSignatures();
        _storage.SetItem($"recovery-signatures-{DateTime.UtcNow.Ticks}", sigs);

        _storage.RemoveItem("recovery-signatures");
    }
}