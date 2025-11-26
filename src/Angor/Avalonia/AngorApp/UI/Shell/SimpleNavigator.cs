using Zafiro.UI.Navigation;
using Zafiro.UI.Navigation.Sections;

namespace AngorApp.UI.NewShell;

public class SimpleNavigator : INavigator
{
    public SimpleNavigator(IObservable<ISection> selectedSection)
    {
        var observable = selectedSection.Select(section => section.Content).Switch();
        observable.Subscribe(o => { });
        Content = observable;
    }
    
    public IObservable<object?> Content { get; }
    public IEnhancedCommand<Result> Back { get; }
    public Task<Result<Unit>> Go(Func<object> factory)
    {
        throw new NotImplementedException();
    }

    public Task<Result<Unit>> Go(Type type)
    {
        throw new NotImplementedException();
    }

    public Task<Result<Unit>> GoBack()
    {
        throw new NotImplementedException();
    }

    public NavigationBookmark CreateBookmark()
    {
        throw new NotImplementedException();
    }

    public void CreateBookmark(string name)
    {
        throw new NotImplementedException();
    }

    public Task<Result<Unit>> GoBackTo(NavigationBookmark bookmark)
    {
        throw new NotImplementedException();
    }

    public Task<Result<Unit>> GoBackTo(string name)
    {
        throw new NotImplementedException();
    }
}