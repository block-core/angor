using AngorApp.UI.Sections.FundedV2.Common.Model;
using AngorApp.UI.Sections.FundedV2.Investment.Manage;
using AngorApp.UI.Sections.Shared.ProjectV2;

namespace AngorApp.UI.Sections.FundedV2.Investment.Model
{
    public class InvestmentFundedSample : IInvestmentFunded
    {
        public IInvestmentProject Project { get; } = new InvestmentProjectSample();
        public IInvestmentInvestorData InvestorData { get; } = new InvestmentInvestorDataSample();

        IProject IFunded.Project => Project;
        IInvestorData IFunded.InvestorData => InvestorData;
    }
}
