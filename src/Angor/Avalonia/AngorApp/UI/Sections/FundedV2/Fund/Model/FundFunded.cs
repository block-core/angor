using AngorApp.UI.Sections.FundedV2.Common.Model;
using AngorApp.UI.Sections.Shared.ProjectV2;

namespace AngorApp.UI.Sections.FundedV2.Fund.Model
{
    public record FundFunded(IFundProject Project, IFundInvestorData InvestorData) : IFundFunded
    {
        IProject IFunded.Project => Project;
        IInvestorData IFunded.InvestorData => InvestorData;
    }
}
