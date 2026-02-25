using AngorApp.Model.Funded.Shared.Model;
using AngorApp.Model.ProjectsV2.FundProject;

namespace AngorApp.Model.Funded.Fund.Model
{
    public interface IFundFunded : IFunded
    {
        new IFundProject Project { get; }
        new IFundInvestorData InvestorData { get; }
    }
}
