using AngorApp.UI.Sections.FundedV2.Common.Model;
using AngorApp.UI.Sections.Shared.ProjectV2;

namespace AngorApp.UI.Sections.FundedV2.Investment.Model
{
    public interface IInvestmentFunded : IFunded
    {
        new IInvestmentProject Project { get; }
        new IInvestmentInvestorData InvestorData { get; }
    }
}
