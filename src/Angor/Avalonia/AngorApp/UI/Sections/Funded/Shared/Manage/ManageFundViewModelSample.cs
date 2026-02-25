using AngorApp.Model.Funded.Fund.Model;
using AngorApp.Model.Funded.Shared.Model;

namespace AngorApp.UI.Sections.Funded.Shared.Manage
{
    public class ManageFundViewModelSample : IManageViewModel
    {
        public IFunded Funded { get; } = new FundFundedSample();
    }
}