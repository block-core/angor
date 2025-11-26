using AngorApp.UI.Sections.New;
using AngorApp.UI.Sections.Wallet.Main;
using Microsoft.Extensions.DependencyInjection;
using Zafiro.Avalonia.Controls.Shell;
using Zafiro.UI.Navigation;
using Zafiro.UI.Navigation.Sections;

namespace AngorApp.UI.NewShell;

public partial class ShellViewModel : ReactiveObject, IShellViewModel
{
    public ShellViewModel(IDictionary<string, ISection> sections)
    {
        SidebarSections = [sections["home"], sections["wallet"]];
        Navigator = new SimpleNavigator(this.WhenAnyValue(sample => sample.SelectedSection).WhereNotNull());
    }
    
    public IEnumerable<ISection> SidebarSections { get; }
    public INavigator Navigator { get; }

    [Reactive]
    private ISection selectedSection;
}