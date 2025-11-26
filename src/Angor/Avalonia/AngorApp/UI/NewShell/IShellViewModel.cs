using Zafiro.UI.Navigation;
using Zafiro.UI.Navigation.Sections;

namespace AngorApp.UI.NewShell;

public interface IShellViewModel
{
    IEnumerable<ISection> SidebarSections { get; }
    public ISection SelectedSection { get; set; }
    public INavigator Navigator { get; }
}