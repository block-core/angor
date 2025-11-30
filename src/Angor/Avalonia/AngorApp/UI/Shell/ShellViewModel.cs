using System.Linq;
using AngorApp.UI.Shell;
using Zafiro.UI.Navigation.Sections;

namespace AngorApp.UI.NewShell;

public partial class ShellViewModel : ReactiveObject, IShellViewModel
{
    public ShellViewModel(IEnumerable<ISection> sections)
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

    public ReactiveCommand<Unit, ISection> GoToSections { get; set; }

    public IEnumerable<ISection> SidebarSections { get; }

    [Reactive]
    private ISection selectedSection;
}