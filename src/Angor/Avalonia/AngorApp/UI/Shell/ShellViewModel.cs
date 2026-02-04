using System.Linq;
using Zafiro.UI.Navigation.Sections;

namespace AngorApp.UI.Shell;

public partial class ShellViewModel : ReactiveObject, IShellViewModel
{
    [Reactive] private bool isDarkThemeEnabled;
    [Reactive] private ISection selectedSection;
    private readonly Dictionary<string, ISection> sectionsByName;

    public ShellViewModel(IEnumerable<ISection> sections, UIServices uiServices)
    {
        sectionsByName = sections.ToDictionary(root => root.Name, root => root);
        SidebarSections = [
                sectionsByName["Home"], 
                sectionsByName["Funds"], 
                sectionsByName["Find Projects"],
                sectionsByName["Funded"],
                sectionsByName["My Projects"],
                sectionsByName["Funders"],
            ];
        
        SelectedSection = sectionsByName["Home"];
        GoToSettings = ReactiveCommand.Create<ISection>(() => SelectedSection = sectionsByName["Settings"]);
        IsDarkThemeEnabled = uiServices.IsDarkThemeEnabled;
        this.WhenAnyValue<ShellViewModel, bool>(x => x.IsDarkThemeEnabled)
            .BindTo(uiServices, services => services.IsDarkThemeEnabled);
    }

    public ReactiveCommand<Unit, ISection> GoToSettings { get; set; }
    public IEnumerable<ISection> SidebarSections { get; }

    public void SetSection(string sectionName)
    {
        SelectedSection = sectionsByName[sectionName];
    }
}