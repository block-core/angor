using Angor.Shared.Models;

namespace Angor.Client.Services;

public interface ISessionStorage
{
    void StoreProjectInfo(ProjectInfo project);
    ProjectInfo? GetProjectById(string projectId);
}