using System.Windows.Input;

namespace AngorApp.Sections.Portfolio.Penalties;

public interface IPenaltiesViewModel
{
    ICommand GoToRecovery { get; }
}
