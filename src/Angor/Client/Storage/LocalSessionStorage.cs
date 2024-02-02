using Angor.Client.Models;
using Angor.Shared.Models;
using Angor.Shared.Services;
using Blazored.SessionStorage;

namespace Angor.Client.Storage;

public class LocalSessionStorage : ICacheStorage
{
    private ISyncSessionStorageService _sessionStorageService;
    
    private const string BrowseIndexerData = "subscriptions";
    private const string NostrPubKeyMapping = "NostrPubKeyMapping";

    public LocalSessionStorage(ISyncSessionStorageService sessionStorageService)
    {
        _sessionStorageService = sessionStorageService;
    }

    public void StoreProject(Project project)
    {
        _sessionStorageService.SetItem(project.ProjectInfo.ProjectIdentifier,project);
    }

    public ProjectMetadata? GetProjectMetadataByPubkey(string pubkey)
    {
        return _sessionStorageService.GetItem<ProjectMetadata>(pubkey);
    }

    public void StoreProjectMetadata(string pubkey, ProjectMetadata projectMetadata)
    {
        _sessionStorageService.SetItem(pubkey, projectMetadata);
    }

    public bool IsProjectMetadataStorageByPubkey(string pubkey)
    {
        return _sessionStorageService.ContainKey(pubkey);
    }

    public Project? GetProjectById(string projectId)
    {
        return _sessionStorageService.GetItem<Project>(projectId);
    }

    public void AddProjectIdToNostrPubKeyMapping(string npub, string projectId)
    {
        var dictionary = _sessionStorageService.GetItem<Dictionary<string, string>>(NostrPubKeyMapping);
        
        dictionary ??= new ();

        dictionary.TryAdd(npub, projectId);
        
        _sessionStorageService.SetItem(NostrPubKeyMapping,dictionary);
    }

    public string? GetProjectIdByNostrPubKey(string npub)
    {
        var dictionary = _sessionStorageService.GetItem<Dictionary<string, string>>(NostrPubKeyMapping);
        
        if (dictionary is null) return null;

        return dictionary.TryGetValue(npub, out var value) ? value : null;
    }

    public bool IsProjectInStorageById(string projectId)
    {
        return _sessionStorageService.ContainKey(projectId);
    }

    public List<ProjectIndexerData>? GetProjectIndexerData()
    {
        return _sessionStorageService.GetItem<List<ProjectIndexerData>>(BrowseIndexerData);
    }

    public void SetProjectIndexerData(List<ProjectIndexerData> list)
    {
        _sessionStorageService.SetItem(BrowseIndexerData,list);
    }

    public List<UtxoData> GetUnconfirmedInboundFunds()
    {
        return _sessionStorageService.GetItem<List<UtxoData>>("unconfirmed-inbound") ?? new ();
    }

    public List<Outpoint> GetUnconfirmedOutboundFunds()
    {
        return _sessionStorageService.GetItem<List<Outpoint>>("unconfirmed-outbound") ?? new();
    }

    public void SetUnconfirmedInboundFunds(List<UtxoData> unconfirmedInfo)
    {
        _sessionStorageService.SetItem("unconfirmed-inbound", unconfirmedInfo);
    }

    public void SetUnconfirmedOutboundFunds(List<Outpoint> unconfirmedInfo)
    {
        _sessionStorageService.SetItem("unconfirmed-outbound", unconfirmedInfo);
    }
    
    public List<string> GetNamesOfCommunicatorsThatReceivedEose(string subscriptionName)
    {
        return _sessionStorageService.GetItem<List<string>>("Eose" + subscriptionName);
    }

    public void DeleteUnconfirmedInfo()
    {
        _sessionStorageService.RemoveItem("unconfirmed-info");
    }

    public void WipeSession()
    {
        _sessionStorageService.Clear();
    }
}