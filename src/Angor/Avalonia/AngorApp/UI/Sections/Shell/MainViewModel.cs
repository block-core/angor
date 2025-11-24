using AngorApp.Core;
using AngorApp.UI.Shared.Services;
using Zafiro.UI.Shell;

namespace AngorApp.UI.Sections.Shell;

public partial class MainViewModel : ReactiveObject, IMainViewModel
{
    public MainViewModel(IShell shell, UIServices uiServices)
    {
        Shell = shell;
    }

    public IShell Shell { get; }

    public ReactiveCommand<Unit, Unit> OpenHub { get; }
}