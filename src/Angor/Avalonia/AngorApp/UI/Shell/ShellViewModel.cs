using System.Linq;
using Zafiro.UI.Navigation.Sections;

namespace AngorApp.UI.NewShell;

public partial class ShellViewModel : ReactiveObject, IShellViewModel
{
    public ShellViewModel(IEnumerable<INavigationRoot> sections)
    {
        var dict = sections.ToDictionary(root => root.Name, root => root);
        SidebarSections = [dict["Home"], dict["Funds"], dict["Find Projects"]];
        SelectedSection = dict["Home"];
    }
    
    public IEnumerable<INavigationRoot> SidebarSections { get; }

    [Reactive]
    private INavigationRoot selectedSection;
}