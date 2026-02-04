using AngorApp.UI.Sections.Funds.Accounts;

namespace AngorApp.UI.Sections.Funds
{
    public class FundsSectionViewModelSample : IFundsSectionViewModel
    {
        public IAccountsViewModel Accounts { get; } = new AccountsViewModelSample();
    }
}