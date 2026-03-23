using AngorApp.Model.Funded.Investment.Model;
using AngorApp.Model.Funded.Shared.Model;

namespace AngorApp.UI.Sections.Funded.Shared.Manage
{
    public class ManageInvestmentViewModelSample : IManageViewModel
    {
        public ManageInvestmentViewModelSample() : this(new InvestmentFundedSample())
        {
        }

        public ManageInvestmentViewModelSample(IFunded funded)
        {
            Funded = funded;
        }

        public IFunded Funded { get; }
    }
}
