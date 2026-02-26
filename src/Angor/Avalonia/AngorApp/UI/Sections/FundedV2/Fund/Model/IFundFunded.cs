using AngorApp.UI.Sections.FundedV2.Common.Model;
using AngorApp.UI.Sections.Shared.ProjectV2;

namespace AngorApp.UI.Sections.FundedV2.Fund.Model
{
    public interface IFundFunded : IFunded
    {
        new IFundProject Project { get; }
        new IFundInvestorData InvestorData { get; }
    }
}
