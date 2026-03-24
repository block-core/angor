using AngorApp.UI.Sections.Funds.Accounts;
using AngorApp.UI.Sections.Funds.Empty;

namespace AngorApp.UI.Sections.Funds
{
    public class FundsSectionViewModelSample : IFundsSectionViewModel
    {
        public IAccountsViewModel Accounts { get; } = new AccountsViewModelSample();
        public IEmptyViewModel Empty { get; } = new EmptyViewModelSample();
    }
}
