using Angor.Client.Services;
using Angor.Shared.Models;

namespace Angor.Shared.Services;

public interface ICacheStorage
{
    void StoreProjectInfo(ProjectInfo project);
    ProjectInfo? GetProjectById(string projectId);
    bool IsProjectInStorageById(string projectId);
    List<ProjectIndexerData>? GetProjectIndexerData();
    void SetProjectIndexerData(List<ProjectIndexerData> list);
    UnconfirmedInfo GetUnconfirmedInfo();
    void SetUnconfirmedInfo(UnconfirmedInfo unconfirmedInfo);
}