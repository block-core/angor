using AngorApp.Model.Funded.Shared.Model;
using AngorApp.Model.ProjectsV2.InvestmentProject;

namespace AngorApp.Model.Funded.Investment.Model
{
    public interface IInvestmentFunded : IFunded
    {
        new IInvestmentProject Project { get; }
        new IInvestmentInvestorData InvestorData { get; }
    }
}
