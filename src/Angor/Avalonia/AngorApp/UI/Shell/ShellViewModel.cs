using System.Linq;
using AngorApp.UI.Shell;
using Zafiro.UI.Navigation.Sections;

namespace AngorApp.UI.NewShell;

public partial class ShellViewModel : ReactiveObject, IShellViewModel
{
    public ShellViewModel(IEnumerable<INavigationRoot> sections)
    {
        var dict = sections.ToDictionary(root => root.Name, root => root);
        SidebarSections = [
                dict["Home"], 
                dict["Funds"], 
                dict["Find Projects"],
                dict["Funded"],
                dict["My Projects"],
            ];
        
        SelectedSection = dict["Home"];
        GoToSections = ReactiveCommand.Create(() => SelectedSection = dict["Settings"]);
    }

    public ReactiveCommand<Unit, INavigationRoot> GoToSections { get; set; }

    public IEnumerable<INavigationRoot> SidebarSections { get; }

    [Reactive]
    private INavigationRoot selectedSection;
}