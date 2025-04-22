using AngorApp.Sections.Home;
using Zafiro.UI.Navigation.Sections;
using Zafiro.UI.Shell;

namespace AngorApp.Sections.Shell;

public class ShellDesign : IShell
{
    public ShellDesign()
    {
        Sections =
        [
            Section.Content("Home", () => new HomeSectionViewModelDesign(), "svg:/Assets/angor-icon.svg"),
            Section.Separator(),
            Section.Content("Wallet", () => new object(), "fa-wallet"),
            Section.Content("Browse", () => new object(), "fa-magnifying-glass"),
            Section.Content("Portfolio", () => new object(), "fa-hand-holding-dollar"),
            Section.Content("Founder", () => new object(), "fa-money-bills"),
            Section.Separator(),
            Section.Content("Settings", () => new object(), "fa-gear"),
            Section.Command("Angor Hub", ReactiveCommand.Create(() => { }), "fa-magnifying-glass", false),
        ];
    }
    public void GoToSection(string sectionName)
    {
        throw new NotImplementedException();
    }

    public IEnumerable<ISection> Sections { get; }
    public IContentSection SelectedSection { get; set; }
}