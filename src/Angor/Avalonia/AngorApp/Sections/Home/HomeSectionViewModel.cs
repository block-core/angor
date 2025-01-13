using System.Windows.Input;
using AngorApp.Core;
using AngorApp.Model;
using AngorApp.Sections.Shell;
using AngorApp.Services;

namespace AngorApp.Sections.Home;

public class HomeSectionViewModel : ReactiveObject, IHomeSectionViewModel
{
    private readonly IWalletProvider provider;

    public HomeSectionViewModel(IWalletProvider provider, UIServices uiServices, Func<IMainViewModel> getMainViewModel)
    {
        this.provider = provider;
        provider.GetWallet();
        GoToWalletSection = ReactiveCommand.Create(() => getMainViewModel().GoToSection("Wallet"));
        OpenHub = ReactiveCommand.CreateFromTask(() => uiServices.LauncherService.LaunchUri(Constants.AngorHubUri));
        //GoToFounderSection = ReactiveCommand.Create(() => getMainViewModel().GoToSection("Founder"));
    }

    public bool IsWalletSetup => provider.GetWallet().HasValue;
    public ICommand GoToWalletSection { get; }
    public ICommand GoToFounderSection { get; }
    public ICommand OpenHub { get; }
}