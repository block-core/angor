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
            new ContentSection<IHomeSectionViewModel>("Home", Observable.Defer(() => Observable.Return<IHomeSectionViewModel>(new HomeSectionViewModelDesign())), new Icon("svg:/Assets/angor-icon.svg")),
            new SectionSeparator(),
            new ContentSection<object>("Wallet", Observable.Defer(() => Observable.Return(new object())), new Icon("fa-wallet")),
            new ContentSection<object>("Browse", Observable.Defer(() => Observable.Return(new object())), new Icon("fa-magnifying-glass")),
            new ContentSection<object>("Portfolio", Observable.Defer(() => Observable.Return(new object())), new Icon("fa-hand-holding-dollar")),
            new ContentSection<object>("Founder", Observable.Defer(() => Observable.Return(new object())), new Icon("fa-money-bills")),
            new SectionSeparator(),
            new ContentSection<object>("Settings", Observable.Defer(() => Observable.Return(new object())), new Icon("fa-gear")),
            new CommandSection("Angor Hub", ReactiveCommand.Create(() => { }), new Icon("fa-magnifying-glass")) { IsPrimary = false },
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