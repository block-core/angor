using Angor.Client.Services;
using Angor.Shared.Models;
using Angor.Shared.Services;
using Blazored.SessionStorage;

namespace Angor.Client.Storage;

public class LocalSessionStorage : ICacheStorage
{
    private ISyncSessionStorageService _sessionStorageService;
    
    private const string BrowseIndexerData = "subscriptions";

    public LocalSessionStorage(ISyncSessionStorageService sessionStorageService)
    {
        _sessionStorageService = sessionStorageService;
    }

    public void StoreProjectInfo(ProjectInfo project)
    {
        _sessionStorageService.SetItem(project.ProjectIdentifier,project);
    }

    public ProjectInfo? GetProjectById(string projectId)
    {
        return _sessionStorageService.GetItem<ProjectInfo>(projectId);
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

    public void DeleteUnconfirmedInfo()
    {
        _sessionStorageService.RemoveItem("unconfirmed-info");
    }

    public void WipeSession()
    {
        _sessionStorageService.Clear();
    }
}