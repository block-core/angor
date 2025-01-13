using AngorApp.Sections.Browse;
using AngorApp.Sections.Home;
using AngorApp.Sections.Wallet;
using AngorApp.Services;
using CSharpFunctionalExtensions;
using Zafiro.Avalonia.Controls.Navigation;

namespace AngorApp.Sections.Shell;

public class MainViewModelDesign : IMainViewModel
{
    public MainViewModelDesign()
    {
        Sections =
        [
            new Section("Home", new HomeSectionViewModelDesign(), "svg:/Assets/angor-icon.svg"),
            new Separator(),
            new Section("Wallet", null, "fa-wallet"),
            new Section("Browse", null, "fa-magnifying-glass"),
            new Section("Portfolio", null, "fa-hand-holding-dollar"),
            new Section("Founder", null, "fa-money-bills"),
            new Separator(),
            new Section("Settings", null, "fa-gear"),
            new CommandSection("Angor Hub", null , "fa-magnifying-glass") { IsPrimary = false }
        ];
    }

    public ReactiveCommand<Unit, Unit> OpenHub { get; }
    public IEnumerable<SectionBase> Sections { get; }
    public Section SelectedSection { get; set; }
}