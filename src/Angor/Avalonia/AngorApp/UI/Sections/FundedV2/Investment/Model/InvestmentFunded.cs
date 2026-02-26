using AngorApp.UI.Sections.FundedV2.Common.Model;
using AngorApp.UI.Sections.Shared.ProjectV2;

namespace AngorApp.UI.Sections.FundedV2.Investment.Model
{
    public record InvestmentFunded(IInvestmentProject Project, IInvestmentInvestorData InvestorData) : IInvestmentFunded
    {
        IProject IFunded.Project => Project;
        IInvestorData IFunded.InvestorData => InvestorData;
    }
}
