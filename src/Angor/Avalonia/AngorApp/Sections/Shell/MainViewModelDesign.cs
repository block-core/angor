using AngorApp.Sections.Home;
using AngorApp.Sections.Shell.Sections;
using Separator = AngorApp.Sections.Shell.Sections.Separator;

namespace AngorApp.Sections.Shell;

public class MainViewModelDesign : IMainViewModel
{
    public MainViewModelDesign()
    {
        Sections =
        [
            Section.Create("Home", () => new HomeSectionViewModelDesign(), "svg:/Assets/angor-icon.svg"),
            new Separator(),
            Section.Create("Wallet", () => new object(), "fa-wallet"),
            Section.Create("Browse", () => new object(), "fa-magnifying-glass"),
            Section.Create("Portfolio", () => new object(), "fa-hand-holding-dollar"),
            Section.Create("Founder", () => new object(), "fa-money-bills"),
            new Separator(),
            Section.Create("Settings", () => new object(), "fa-gear"),
            new CommandSection("Angor Hub", ReactiveCommand.Create(() => { }), "fa-magnifying-glass")
                { IsPrimary = false }
        ];
    }

    public ReactiveCommand<Unit, Unit> OpenHub { get; }
    public IEnumerable<SectionBase> Sections { get; }
    public IContentSection SelectedSection { get; set; }

    public void GoToSection(string sectionName)
    {
    }
}