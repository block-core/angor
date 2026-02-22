using Zafiro.UI.Navigation.Sections;

namespace AngorApp.UI.Shell
{
    public interface IShellViewModel
    {
        IEnumerable<ISection> SidebarSections { get; }
        ISection SelectedSection { get; set; }
        ReactiveCommand<Unit, ISection> GoToSettings { get; set; }
        bool IsDarkThemeEnabled { get; set; }
        IReadOnlyCollection<IWallet> Wallets { get; }
        IWallet? CurrentWallet { get; set; }
        IAmountUI? TotalInvested { get; }
        void SetSection(string sectionName);
    }
}