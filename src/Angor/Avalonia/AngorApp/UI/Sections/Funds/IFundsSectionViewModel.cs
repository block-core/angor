using AngorApp.UI.Sections.Funds.Accounts;

namespace AngorApp.UI.Sections.Funds
{
    public interface IFundsSectionViewModel
    {
        public IAccountsViewModel Accounts { get; }
    }
}