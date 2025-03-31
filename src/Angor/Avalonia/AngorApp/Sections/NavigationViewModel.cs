using Zafiro.Avalonia.Controls.Navigation;

namespace AngorApp.Sections;

public class NavigationViewModel : ReactiveObject
{
    public NavigationViewModel(INavigator navigator, Func<object> viewModel)
    {
        Navigator = navigator;
        Create = ReactiveCommand.Create(() => Navigator.Go(() => viewModel()));
        Create.Execute().Subscribe();
    }

    public INavigator Navigator { get; set; }
   
    public ReactiveCommand<Unit, Unit> Create { get; }
}