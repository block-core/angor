using Angor.Shared.Models;

namespace Angor.Client.Services;

public interface ISessionStorage
{
    void StoreProjectInfo(ProjectInfo project);
    void AddProjectToSubscribedList(string nostrPubKey);
    List<string> GetProjectSubscribedList();
    void SetProjectSubscribedList(List<string> nostrPubKeys);
    bool IsProjectInSubscribedList(string nostrPubKey);
    ProjectInfo? GetProjectById(string projectId);
    bool IsProjectInStorageById(string projectId);
    void StoreProjectInfoEventId(string eventId, string projectInfo);
    ProjectInfo GetProjectInfoByEventId(string eventId);
}