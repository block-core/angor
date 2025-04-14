using AngorApp.Core;
using AngorApp.UI.Services;
using Zafiro.UI.Shell;

namespace AngorApp.Sections.Shell;

public partial class MainViewModel : ReactiveObject, IMainViewModel
{
    public MainViewModel(IShell shell, UIServices uiServices)
    {
        Shell = shell;
        OpenHub = ReactiveCommand.CreateFromTask(() => uiServices.LauncherService.LaunchUri(Constants.AngorHubUri));
    }

    public IShell Shell { get; }

    public ReactiveCommand<Unit, Unit> OpenHub { get; }
}