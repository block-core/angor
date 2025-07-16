using System.Windows.Input;
using AngorApp.Sections.Portfolio.Recover;
using Zafiro.UI.Navigation;

namespace AngorApp.Sections.Portfolio.Penalties;

public class PenaltiesViewModel : ReactiveObject, IPenaltiesViewModel
{
    public PenaltiesViewModel(INavigator navigator)
    {
        GoToRecovery = ReactiveCommand.Create(() => navigator.Go<IRecoverViewModel>());
    }

    public ICommand GoToRecovery { get; }
}