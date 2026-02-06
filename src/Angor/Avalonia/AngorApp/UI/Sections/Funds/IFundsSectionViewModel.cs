using AngorApp.UI.Sections.Funds.Accounts;
using AngorApp.UI.Sections.Funds.Empty;

namespace AngorApp.UI.Sections.Funds
{
    public interface IFundsSectionViewModel
    {
        public IAccountsViewModel Accounts { get; }
        public IEmptyViewModel Empty { get; }
    }
}
