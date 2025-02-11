using System.Windows.Input;
using Angor.UI.Model;
using AngorApp.Core;
using AngorApp.Sections.Shell;
using AngorApp.Services;

namespace AngorApp.Sections.Home;

public class HomeSectionViewModel(
    IActiveWallet activeWallet,
    UIServices uiServices,
    Func<IMainViewModel> getMainViewModel)
    : ReactiveObject, IHomeSectionViewModel
{
    //GoToFounderSection = ReactiveCommand.Create(() => getMainViewModel().GoToSection("Founder"));

    public bool IsWalletSetup => activeWallet.Current.HasValue;
    public ICommand GoToWalletSection { get; } = ReactiveCommand.Create(() => getMainViewModel().GoToSection("Wallet"), activeWallet.HasWallet);
    public ICommand GoToFounderSection { get; }
    public ICommand OpenHub { get; } = ReactiveCommand.CreateFromTask(() => uiServices.LauncherService.LaunchUri(Constants.AngorHubUri));
}