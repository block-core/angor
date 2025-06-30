using System.Windows.Input;
using AngorApp.Core;
using AngorApp.Sections.Portfolio.Recover;
using AngorApp.UI.Services;
using ReactiveUI.SourceGenerators;
using Zafiro.UI.Navigation;

namespace AngorApp.Sections.Portfolio.Penalties;

public partial class PenaltiesViewModel : ReactiveObject, IPenaltiesViewModel
{
    public PenaltiesViewModel(INavigator navigator)
    {
        GoToRecovery = ReactiveCommand.Create(() => navigator.Go<IRecoverViewModel>());
    }

    public ICommand GoToRecovery { get; }
}
