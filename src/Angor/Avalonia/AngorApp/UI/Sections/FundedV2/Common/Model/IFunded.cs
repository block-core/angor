using AngorApp.UI.Sections.Shared.ProjectV2;

namespace AngorApp.UI.Sections.FundedV2.Common.Model
{
    public interface IFunded
    {
        public IProject Project { get; }
        public IInvestorData InvestorData { get; }
    }
}