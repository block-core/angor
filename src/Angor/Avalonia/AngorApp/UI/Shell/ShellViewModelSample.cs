using AngorApp.UI.Sections.Home;
using Zafiro.Avalonia.Controls.Shell;
using Zafiro.UI.Navigation.Sections;

namespace AngorApp.UI.Shell;

public partial class ShellViewModelSample : ReactiveObject, IShellViewModel
{
    public ShellViewModelSample()
    {
        var home = new SimpleSection()
        {
            Content = new HomeSectionView(),
            FriendlyName = "Home",
            Name = "Home",
            Icon = new Icon("fa-home"),
            SortOrder = 0,
        };
        var funds = new SimpleSection()
        {
            Content = "Content 2",
            FriendlyName = "Funds",
            Name = "Funds",
            Icon = new Icon("fa-regular fa-credit-card"),
            SortOrder = 1,
        };
        var find = new SimpleSection()
        {
            Content = "Content 3",
            FriendlyName = "Find Projects",
            Name = "Find Projects",
            Group = new SectionGroup("INVESTOR"),
            Icon = new Icon("fa-magnifying-glass"),
        };
        var funded = new SimpleSection()
        {
            FriendlyName = "Funded",
            Group = new SectionGroup("INVESTOR"),
            Name = "Funded",
            Icon = new Icon("fa-arrow-trend-up"),
        };
        var myProjects = new SimpleSection()
        {
            FriendlyName = "My Projects",
            Group = new SectionGroup("FOUNDER"),
            Name = "My Projects",
            Icon = new Icon("fa-regular fa-file-lines"),
        };

        SidebarSections = [home, funds, find, funded, myProjects];
    }
    
    public IEnumerable<ISection> SidebarSections { get; }
    public ReactiveCommand<Unit, ISection> GoToSettings { get; set; }
    public bool IsDarkThemeEnabled { get; set; }
    public void SetSection(string sectionName)
    {
        throw new NotSupportedException();
    }

    [Reactive]
    private ISection selectedSection;
}