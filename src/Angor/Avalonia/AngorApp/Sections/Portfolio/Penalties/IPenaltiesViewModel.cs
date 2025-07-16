using System.Windows.Input;
using Zafiro.UI.Commands;

namespace AngorApp.Sections.Portfolio.Penalties;

public interface IPenaltiesViewModel
{
    ICommand GoToRecovery { get; }
    public IEnumerable<IPenaltyViewModel> Penalties { get; }
    public EnhancedCommand<Result<IEnumerable<IPenaltyViewModel>>> Load { get; }
}