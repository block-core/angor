using System.Threading.Tasks;
using Angor.UI.Model.Implementation.Wallet.Password;
using AngorApp.Sections.Home;
using Zafiro.UI.Navigation;
using Zafiro.UI.Navigation.Sections;
using Zafiro.UI.Shell;

namespace AngorApp.Sections.Shell;

public class ShellDesign : IShell
{
    public ShellDesign()
    {
        Sections =
        [
            Section.Content("Home", Observable.Defer(() => Observable.Return<IHomeSectionViewModel>(new HomeSectionViewModelDesign())), new Icon("svg:/Assets/angor-icon.svg")),
            Section.Separator(),
            Section.Content("Wallet", Observable.Defer(() => Observable.Return(new object())), new Icon("fa-wallet")),
            Section.Content("Browse", Observable.Defer(() => Observable.Return(new object())), new Icon("fa-magnifying-glass")),
            Section.Content("Portfolio", Observable.Defer(() => Observable.Return(new object())), new Icon("fa-hand-holding-dollar")),
            Section.Content("Founder", Observable.Defer(() => Observable.Return(new object())), new Icon("fa-money-bills")),
            Section.Separator(),
            Section.Content("Settings", Observable.Defer(() => Observable.Return(new object())), new Icon("fa-gear")),
            Section.Command("Angor Hub", ReactiveCommand.Create(() => { }), new Icon("fa-magnifying-glass"), false),
        ];
    }

    public IObservable<object?> Content { get; }

    public void GoToSection(string sectionName)
    {
        throw new NotImplementedException();
    }

    public object Header { get; set; }

    public IObservable<object> ContentHeader => Observable.Return("Header content");
    public IEnumerable<ISection> Sections { get; }
    public IContentSection SelectedSection { get; set; }
    public INavigator Navigator { get; }
}