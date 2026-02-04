using AngorApp.UI.Sections.Funds.Accounts;
using AngorApp.UI.Sections.Wallet.Main;
using Zafiro.UI.Shell.Utils;

namespace AngorApp.UI.Sections.Funds;

[Section("Funds", icon: "fa-regular fa-credit-card", sortIndex: 1)]
public class FundsSectionViewModel(IAccountsViewModel accounts, IWalletSectionViewModel oldWalletSection) : IFundsSectionViewModel
{
    public IAccountsViewModel Accounts { get; } = accounts;
}