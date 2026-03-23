using AngorApp.UI.Sections.Funds.Accounts;
using AngorApp.UI.Sections.Funds.Empty;
using Zafiro.UI.Shell.Utils;

namespace AngorApp.UI.Sections.Funds;

[Section("Funds", icon: "fa-regular fa-credit-card", sortIndex: 1)]
public class FundsSectionViewModel(IAccountsViewModel accounts, IEmptyViewModel empty) : IFundsSectionViewModel
{
    public IAccountsViewModel Accounts { get; } = accounts;
    public IEmptyViewModel Empty { get; } = empty;
}
