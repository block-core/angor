using AngorApp.UI.Sections.Home;
using Zafiro.UI.Navigation;
using Zafiro.UI.Navigation.Sections;
using Zafiro.UI.Shell;

namespace AngorApp.UI.Sections.Shell;

public class ShellSample : IShell
{
    public ShellSample()
    {
        Sections =
        [
            CreateSection("Home", () => new HomeSectionViewModelSample(), new Icon("svg:/Assets/angor-icon.svg")),
            CreateSection("Wallet", () => new object(), new Icon("fa-wallet")),
            CreateSection("Browse", () => new object(), new Icon("fa-magnifying-glass")),
            CreateSection("Portfolio", () => new object(), new Icon("fa-hand-holding-dollar")),
            CreateSection("Founder", () => new object(), new Icon("fa-money-bills")),
            CreateSection("Settings", () => new object(), new Icon("fa-gear")),
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
    public ISection SelectedSection { get; set; }
    public INavigator Navigator { get; }

    private static ContentSection<T> CreateSection<T>(string name, Func<T> factory, Icon icon) where T : class
    {
        var content = Observable.Defer(() => Observable.Return(factory()));
        return new ContentSection<T>(name, content, icon, navigator => navigator.Go(() => factory()!));
    }
}
