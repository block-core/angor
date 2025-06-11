using System.Reactive.Threading.Tasks;
using System.Threading.Tasks;
using AngorApp.Features.Invest;
using AngorApp.UI.Services;
using Zafiro.Avalonia.Dialogs;
using Zafiro.UI;
using Zafiro.UI.Commands;

namespace AngorApp.Sections.Browse.Details;

public class ProjectDetailsViewModel : ReactiveObject, IProjectDetailsViewModel
{
    private readonly IProject project;
    private readonly InvestWizard investWizard;
    private readonly UIServices uiServices;

    public ProjectDetailsViewModel(IProject project, InvestWizard investWizard, UIServices uiServices)
    {
        this.project = project;
        this.investWizard = investWizard;
        this.uiServices = uiServices;
        Invest = ReactiveCommand.CreateFromTask(DoInvest).Enhance();

        Invest.HandleErrorsWith(uiServices.NotificationService, "Investment failed");
    }

    public object Icon => project.Picture;
    public object Picture => project.Banner;

    public IEnhancedCommand<Result> Invest { get; }

    public IEnumerable<INostrRelay> Relays { get; } =
    [
        new NostrRelayDesign
        {
            Uri = new Uri("wss://relay.angor.io")
        },
        new NostrRelayDesign
        {
            Uri = new Uri("wss://relay2.angor.io")
        }
    ];

    public double TotalDays { get; } = 119;
    public double TotalInvestment { get; } = 1.5d;
    public double CurrentDays { get; } = 11;
    public double CurrentInvestment { get; } = 0.79d;
    public IProject Project => project;

    private async Task<Result> DoInvest()
    {
        var getCurrentResult = await uiServices.WalletRoot.GetDefaultWalletAndActivate().Tap(r => r.ExecuteNoValue(ShowNoWalletMessage));
        return await getCurrentResult
            .Map(maybeWallet => maybeWallet
                .Bind(wallet => investWizard.Invest(wallet, project)));
    }

    private async Task<Maybe<Unit>> ShowNoWalletMessage()
    {
        await uiServices.Dialog.ShowMessage("No wallet found", "Please create or recover a wallet to invest in this project.");
        return Maybe<Unit>.None;
    }
}