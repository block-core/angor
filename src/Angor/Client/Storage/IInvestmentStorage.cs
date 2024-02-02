using Angor.Client.Models;

namespace Angor.Client.Storage;

public interface IInvestmentStorage
{
    void AddInvestmentProject(InvestorProject project);
    void RemoveInvestmentProject(string projectId);
    void UpdateInvestmentProject(InvestorProject project);
    List<InvestorProject> GetInvestmentProjects();
    void DeleteInvestmentProjects();
}