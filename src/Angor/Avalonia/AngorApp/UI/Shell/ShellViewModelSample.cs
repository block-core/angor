using AngorApp.UI.Sections.New;
using Zafiro.Avalonia.Controls.Shell;
using Zafiro.UI.Navigation;
using Zafiro.UI.Navigation.Sections;

namespace AngorApp.UI.NewShell;

public partial class ShellViewModelSample : ReactiveObject, IShellViewModel
{
    public ShellViewModelSample()
    {
        var home = new SimpleSection()
        {
            ContentValue = new HomeSectionView(),
            FriendlyName = "Home",
            Name = "Home",
            Icon = new Icon("fa-home"),
            SortOrder = 0,
        };
        var funds = new SimpleSection()
        {
            ContentValue = "Content 2",
            FriendlyName = "Funds",
            Name = "Funds",
            Icon = new Icon("fa-regular fa-credit-card"),
            SortOrder = 1,
        };
        var find = new SimpleSection()
        {
            ContentValue = "Content 3",
            FriendlyName = "Find Projects",
            Name = "Find Projects",
            Group = new SectionGroup("INVESTOR"),
            Icon = new Icon("fa-magnifying-glass"),
        };
        var funded = new SimpleSection()
        {
            ContentValue = "Content 4",
            FriendlyName = "Funded",
            Group = new SectionGroup("INVESTOR"),
            Name = "Funded",
            Icon = new Icon("fa-arrow-trend-up"),
        };
        var myProjects = new SimpleSection()
        {
            ContentValue = "Content 5",
            FriendlyName = "My Projects",
            Group = new SectionGroup("FOUNDER"),
            Name = "My Projects",
            Icon = new Icon("fa-regular fa-file-lines"),
        };

        SidebarSections = [home, funds, find, funded, myProjects];
    }
    
    public IEnumerable<INavigationRoot> SidebarSections { get; }
    public ReactiveCommand<Unit, INavigationRoot> GoToSections { get; set; }

    [Reactive]
    private INavigationRoot selectedSection;
}