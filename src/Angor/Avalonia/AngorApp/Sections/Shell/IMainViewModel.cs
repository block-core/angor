using Zafiro.UI.Shell;

namespace AngorApp.Sections.Shell;

public interface IMainViewModel
{
    ReactiveCommand<Unit, Unit> OpenHub { get; }
    IShell Shell { get; }
}