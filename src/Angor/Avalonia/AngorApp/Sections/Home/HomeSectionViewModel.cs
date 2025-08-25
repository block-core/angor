using System.Windows.Input;
using Angor.UI.Model;
using AngorApp.Core;
using AngorApp.UI.Services;
using ReactiveUI.SourceGenerators;
using Zafiro.UI.Commands;
using Zafiro.UI.Navigation;
using Zafiro.UI.Shell;

namespace AngorApp.Sections.Home;

public partial class HomeSectionViewModel : ReactiveObject, IHomeSectionViewModel
{
    public HomeSectionViewModel(UIServices uiServices, INavigator navigator, IShell shell)
    {
        //GoToWalletSection = ReactiveCommand.Create(() => getMainViewModel().GoToSection("Wallet"), activeWallet.HasWallet);
        OpenHub = ReactiveCommand.CreateFromTask(() => uiServices.LauncherService.LaunchUri(Constants.AngorHubUri));
        GoToAngorFlow = ReactiveCommand.Create(() => navigator.Go(() => new AngorFlowViewModel())).Enhance();
        GoToSection = ReactiveCommand.Create((string s) => shell.GoToSection(s));
    }

    public ICommand OpenHub { get; }
    public IEnhancedCommand GoToAngorFlow { get; set; }
    public ReactiveCommand<string, Unit> GoToSection { get; }
}