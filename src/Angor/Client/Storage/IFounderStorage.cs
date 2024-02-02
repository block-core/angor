using Angor.Client.Models;

namespace Angor.Client.Storage;

public interface IFounderStorage
{
    void AddFounderProject(params FounderProject[] projects);
    List<FounderProject> GetFounderProjects();
    void UpdateFounderProject(FounderProject project);
    void DeleteFounderProjects();
}