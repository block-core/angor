using Zafiro.UI.Navigation.Sections;

namespace AngorApp.UI.NewShell;

public interface IShellViewModel
{
    IEnumerable<INavigationRoot> SidebarSections { get; }
    public INavigationRoot SelectedSection { get; set; }
    ReactiveCommand<Unit, INavigationRoot> GoToSections { get; set; }
}