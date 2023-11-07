using Angor.Client.Services;
using Angor.Shared.Models;
using Blazored.SessionStorage;

namespace Angor.Client.Storage;

public class LocalSessionStorage : ISessionStorage
{
    private ISyncSessionStorageService _sessionStorageService;

    private const string NostrKeyEventStreamSubscription = "subscriptions";

    public LocalSessionStorage(ISyncSessionStorageService sessionStorageService)
    {
        _sessionStorageService = sessionStorageService;
    }

    public void StoreProjectInfo(ProjectInfo project)
    {
        _sessionStorageService.SetItem(project.ProjectIdentifier,project);
    }

    public void AddProjectToSubscribedList(string nostrPubKey)
    {
        var list = _sessionStorageService.GetItem<List<string>>(NostrKeyEventStreamSubscription) ?? new List<string>();

        list.Add(nostrPubKey);

        _sessionStorageService.SetItem(NostrKeyEventStreamSubscription, list);
    }

    public List<string> GetProjectSubscribedList()
    {
        return _sessionStorageService.GetItem<List<string>>(NostrKeyEventStreamSubscription) ?? new List<string>();
    }
    public void SetProjectSubscribedList(List<string> list)
    {
        _sessionStorageService.SetItem<List<string>>(NostrKeyEventStreamSubscription,list);
    }

    public bool IsProjectInSubscribedList(string nostrPubKey)
    {
        var list = _sessionStorageService.GetItem<List<string>>(NostrKeyEventStreamSubscription) ?? new List<string>();

        return list.Contains(nostrPubKey);
    }

    public ProjectInfo? GetProjectById(string projectId)
    {
        return _sessionStorageService.GetItem<ProjectInfo>(projectId);
    }
    public bool IsProjectInStorageById(string projectId)
    {
        return _sessionStorageService.ContainKey(projectId);
    }

    public void StoreProjectInfoEventId(string eventId, string projectInfo)
    {
        _sessionStorageService.SetItem(eventId,projectInfo);
    }

    public ProjectInfo GetProjectInfoByEventId(string eventId)
    {
        var projectIdentifier = _sessionStorageService.GetItem<string>(eventId);

        return _sessionStorageService.GetItem<ProjectInfo>(projectIdentifier);
    }
}