using AngorApp.UI.Sections.Browse;
using AngorApp.UI.Sections.Home;
using Zafiro.UI.Navigation;
using Zafiro.UI.Navigation.Sections;

namespace AngorApp.UI.NewShell;

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
    public INavigator Navigator { get; }
}