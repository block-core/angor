using Angor.Shared.Models;

namespace Angor.Client.Services;

public interface ISessionStorage
{
    void StoreProjectInfo(ProjectInfo project);
    void AddProjectToSubscribedList(string nostrPubKey);
    bool IsProjectInSubscribedList(string nostrPubKey);
    ProjectInfo? GetProjectById(string projectId);
}