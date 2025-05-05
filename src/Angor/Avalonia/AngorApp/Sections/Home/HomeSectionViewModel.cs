using System.Windows.Input;
using Angor.UI.Model;
using AngorApp.Core;
using AngorApp.UI.Services;
using ReactiveUI.SourceGenerators;

namespace AngorApp.Sections.Home;

public partial class HomeSectionViewModel : ReactiveObject, IHomeSectionViewModel
{
    public HomeSectionViewModel(UIServices uiServices)
    {
        //GoToWalletSection = ReactiveCommand.Create(() => getMainViewModel().GoToSection("Wallet"), activeWallet.HasWallet);
        OpenHub = ReactiveCommand.CreateFromTask(() => uiServices.LauncherService.LaunchUri(Constants.AngorHubUri));
        isWalletSetupHelper = uiServices.ActiveWallet.HasWallet.ToProperty(this, x => x.IsWalletSetup);
    }

    [ObservableAsProperty] private bool isWalletSetup;

    public ICommand GoToWalletSection { get; }

    public ICommand GoToFounderSection { get; }

    public ICommand OpenHub { get; }
}