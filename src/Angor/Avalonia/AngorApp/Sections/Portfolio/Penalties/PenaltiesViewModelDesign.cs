using System.Windows.Input;

namespace AngorApp.Sections.Portfolio.Penalties;

public class PenaltiesViewModelDesign : IPenaltiesViewModel
{
    public PenaltiesViewModelDesign()
    {
        GoToRecovery = ReactiveCommand.Create(() => { });
    }

    public ICommand GoToRecovery { get; }
}
