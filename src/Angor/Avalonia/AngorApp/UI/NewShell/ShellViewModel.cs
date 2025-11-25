using AngorApp.UI.Sections.Browse;
using AngorApp.UI.Sections.Home;
using Zafiro.UI.Navigation;
using Zafiro.UI.Navigation.Sections;

namespace AngorApp.UI.NewShell;

public interface IShellViewModel
{
    IEnumerable<ISection> SidebarSections { get; }
    public ISection SelectedSection { get; set; }
}

public partial class ShellViewModelSample : ReactiveObject, IShellViewModel
{
    public ShellViewModelSample()
    {
        SidebarSections = new SectionsBuilder()
            .Add<IHomeSectionViewModel>("Home", new Icon("fa-home"))
            .Add<IHomeSectionViewModel>("Funds", new Icon("fa-regular fa-credit-card"))
            .Add<IBrowseSectionViewModel>("Find projects", new Icon("fa-magnifying-glass"), new SectionGroup("Investor", "INVESTOR"))
            .Add<IHomeSectionViewModel>("Funded", new Icon("fa-arrow-trend-up"), new SectionGroup("Investor", "INVESTOR"))
            .Add<IHomeSectionViewModel>("My Projects", new Icon("fa-regular fa-file-lines"), new SectionGroup("Founder", "FOUNDER"))
            .Build();
    }
    
    public IEnumerable<ISection> SidebarSections { get; }
    [Reactive]
    private ISection selectedSection;
}

public class ShellViewModel : ReactiveObject, IShellViewModel
{
    public ShellViewModel()
    {
        this.SidebarSections = new SectionsBuilder()
            .Add<HomeSectionViewModel>("Home", "fa-home")
            .Build();
    }

    public IEnumerable<ISection> SidebarSections { get; }
    public ISection SelectedSection { get; set; }
}