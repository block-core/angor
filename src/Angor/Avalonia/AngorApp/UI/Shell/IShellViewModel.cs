using Zafiro.UI.Navigation.Sections;

namespace AngorApp.UI.Shell;

public interface IShellViewModel
{
    IEnumerable<ISection> SidebarSections { get; }
    public ISection SelectedSection { get; set; }
    ReactiveCommand<Unit, ISection> GoToSettings { get; set; }
    bool IsDarkThemeEnabled { get; set; }
    void SetSection(string sectionName);
}