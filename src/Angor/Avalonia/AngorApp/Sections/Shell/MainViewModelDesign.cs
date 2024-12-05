using AngorApp.Sections.Browse;
using AngorApp.Sections.Home;
using AngorApp.Sections.Wallet;
using AngorApp.Services;

namespace AngorApp.Sections.Shell;

public class MainViewModelDesign : IMainViewModel
{
    public MainViewModelDesign()
    {
        Sections =
        [
            new Section("Home", new HomeViewModel(), "svg:/Assets/angor-icon.svg"),
            new Separator(),
            new Section("Wallet", new WalletViewModel(), "fa-wallet"),
            new Section("Browse", new BrowseViewModel(new UIServices(new NoopLauncherService())), "fa-magnifying-glass"),
            new Section("Portfolio", new WalletViewModel(), "fa-hand-holding-dollar"),
            new Section("Founder", new WalletViewModel(), "fa-money-bills"),
            new Separator(),
            new Section("Settings", new WalletViewModel(), "fa-gear"),
            new CommandSection("Angor Hub", null , "fa-magnifying-glass") { IsPrimary = false }
        ];
    }

    public ReactiveCommand<Unit, Unit> OpenHub { get; }
    public IEnumerable<SectionBase> Sections { get; }
    public Section SelectedSection { get; set; }
}