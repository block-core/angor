using System.Windows.Input;
using AngorApp.Core;
using AngorApp.UI.Shared.Services;
using Zafiro.Avalonia;
using Zafiro.Avalonia.Services;
using Zafiro.UI.Commands;
using Zafiro.UI.Navigation;
using Zafiro.UI.Shell;

namespace AngorApp.UI.Sections.Home;

public class HomeSectionViewModel : ReactiveObject, IHomeSectionViewModel
{
    public HomeSectionViewModel(INavigator navigator, ISectionActions sectionActions, ILauncherService launcherService)
    {
        OpenHub = ReactiveCommand.CreateFromTask(() => launcherService.LaunchUri(Constants.AngorHubUri));
        GoToAngorFlow = ReactiveCommand.Create(() => navigator.Go(() => new AngorFlowViewModel())).Enhance();
        GoToSection = ReactiveCommand.Create((string s) => sectionActions.RequestGoToSection(s));
    }

    public ICommand OpenHub { get; }
    public IEnhancedCommand GoToAngorFlow { get; set; }
    public ReactiveCommand<string, Unit> GoToSection { get; }
}