using Angor.Client.Services;
using Angor.Shared.Models;
using Blazored.SessionStorage;

namespace Angor.Client.Storage;

public class LocalSessionStorage : ISessionStorage
{
    private ISyncSessionStorageService _sessionStorageService;

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
}