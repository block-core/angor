using System.Windows.Input;
using Angor.UI.Model;
using AngorApp.Core;
using AngorApp.UI.Services;
using ReactiveUI.SourceGenerators;
using Zafiro.UI.Commands;
using Zafiro.UI.Navigation;

namespace AngorApp.Sections.Home;

public partial class HomeSectionViewModel : ReactiveObject, IHomeSectionViewModel
{
    public HomeSectionViewModel(UIServices uiServices, INavigator navigator)
    {
        //GoToWalletSection = ReactiveCommand.Create(() => getMainViewModel().GoToSection("Wallet"), activeWallet.HasWallet);
        OpenHub = ReactiveCommand.CreateFromTask(() => uiServices.LauncherService.LaunchUri(Constants.AngorHubUri));
        isWalletSetupHelper = uiServices.ActiveWallet.HasWallet.ToProperty(this, x => x.IsWalletSetup);
        GoToAngorFlow = ReactiveCommand.Create(() => navigator.Go(() => new AngorFlowViewModel())).Enhance();
    }

    [ObservableAsProperty] private bool isWalletSetup;

    public ICommand GoToWalletSection { get; }

    public ICommand GoToFounderSection { get; }

    public ICommand OpenHub { get; }
    public IEnhancedCommand GoToAngorFlow { get; set; }
}