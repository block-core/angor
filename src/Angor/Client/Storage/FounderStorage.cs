using Angor.Client.Models;
using Blazored.LocalStorage;

namespace Angor.Client.Storage;

public class FounderStorage : IFounderStorage
{
    private readonly ISyncLocalStorageService _storage;

    public FounderStorage(ISyncLocalStorageService storage)
    {
        _storage = storage;
    }

    public void AddFounderProject(params FounderProject[] projects)
    {
        var ret = GetFounderProjects();

        ret.AddRange(projects);

        _storage.SetItem("founder-projects", ret.OrderBy(_ => _.ProjectIndex));
    }

    public List<FounderProject> GetFounderProjects()
    {
        var ret = _storage.GetItem<List<FounderProject>>("founder-projects");

        return ret ?? new List<FounderProject>();
    }

    public void UpdateFounderProject(FounderProject project)
    {
        var projects = _storage.GetItem<List<FounderProject>>("founder-projects");
        
        var item = projects.FirstOrDefault(f => f.ProjectInfo.ProjectIdentifier == project.ProjectInfo.ProjectIdentifier);

        if (item != null)
        {
            projects.Remove(item);   
        }
        
        projects.Add(project);
        
        _storage.SetItem("founder-projects", projects.OrderBy(_ => _.ProjectIndex));
    }

    public void DeleteFounderProjects()
    {
        _storage.RemoveItem("founder-projects");
    }
}