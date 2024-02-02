using Angor.Client.Models;
using Blazored.LocalStorage;

namespace Angor.Client.Storage;

public class InvestmentStorage : IInvestmentStorage
{
    private readonly ISyncLocalStorageService _storage;

    public InvestmentStorage(ISyncLocalStorageService storage)
    {
        _storage = storage;
    }

    public void AddInvestmentProject(InvestorProject project)
    {
        var ret = GetInvestmentProjects();

        if (ret.Any(a => a.ProjectInfo?.ProjectIdentifier == project.ProjectInfo.ProjectIdentifier))
        {
            return;
        }

        ret.Add(project);

        _storage.SetItem("projects", ret);
    }

    public void UpdateInvestmentProject(InvestorProject project)
    {
        var ret = GetInvestmentProjects();

        var item = ret.First(_ => _.ProjectInfo?.ProjectIdentifier == project.ProjectInfo.ProjectIdentifier);

        if(!ret.Remove(item)) 
            throw new InvalidOperationException();

        ret.Add(project);

        _storage.SetItem("projects", ret);
    }

    public void RemoveInvestmentProject(string projectId)
    {
        var ret = GetInvestmentProjects();

        var item = ret.First(_ => _.ProjectInfo?.ProjectIdentifier == projectId);

        ret.Remove(item);

        _storage.SetItem("projects", ret);
    }

    public void DeleteInvestmentProjects()
    {
        _storage.RemoveItem("projects");
    }

    public List<InvestorProject> GetInvestmentProjects()
    {
        var ret =  _storage.GetItem<List<InvestorProject>>("projects");

        return ret ?? new List<InvestorProject>();
    }
}