using System.Collections.Generic;
using System.Windows.Input;
using Zafiro.UI;

namespace AngorApp.UI.Sections.Portfolio.Penalties;

public interface IPenaltiesViewModel
{
    ICommand GoToRecovery { get; }
    IReadOnlyCollection<IPenaltyViewModel> Penalties { get; }
    IEnhancedCommand<Result<IEnumerable<IPenaltyViewModel>>> Load { get; }
}
