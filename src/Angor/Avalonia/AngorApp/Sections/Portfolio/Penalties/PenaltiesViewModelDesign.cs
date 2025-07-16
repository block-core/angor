using System.Windows.Input;
using Zafiro.UI.Commands;

namespace AngorApp.Sections.Portfolio.Penalties;

public class PenaltiesViewModelDesign : IPenaltiesViewModel
{
    public PenaltiesViewModelDesign()
    {
        GoToRecovery = ReactiveCommand.Create(() => { });
    }

    public ICommand GoToRecovery { get; }

    public IEnumerable<IPenaltyViewModel> Penalties { get; set; } =
    [
        new PenaltyViewModelDesign(),
        new PenaltyViewModelDesign(),
        new PenaltyViewModelDesign()
    ];

    public EnhancedCommand<Result<IEnumerable<IPenaltyViewModel>>> Load { get; }
}