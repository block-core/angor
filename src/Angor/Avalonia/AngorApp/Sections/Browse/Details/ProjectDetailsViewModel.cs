using System.Reactive.Threading.Tasks;
using System.Threading.Tasks;
using Angor.Contexts.Wallet.Application;
using AngorApp.Features.Invest;
using AngorApp.UI.Services;
using Zafiro.Avalonia.Dialogs;
using Zafiro.UI;

namespace AngorApp.Sections.Browse.Details;

public class ProjectDetailsViewModel : ReactiveObject, IProjectDetailsViewModel
{
    private readonly IProject project;
    private readonly InvestWizard investWizard;
    private readonly UIServices uiServices;

    public ProjectDetailsViewModel(IWalletAppService walletAppService, IProject project, InvestWizard investWizard, UIServices uiServices)
    {
        this.project = project;
        this.investWizard = investWizard;
        this.uiServices = uiServices;
        Invest = ReactiveCommand.CreateFromTask(() =>
        {
            return walletAppService.GetMetadatas()
                .Map(x => x.TryFirst())
                .Bind(maybeMetadata =>
                {
                    if (maybeMetadata.HasValue)
                    {
                        return Result.Success(Observable.FromAsync(() => DoInvest(project, uiServices))
                            .SubscribeOn(RxApp.MainThreadScheduler)
                            .ToTask());
                    }
                    else
                    {
                        return Result.Success(Maybe<Unit>.None);
                    }
                });
        });

        Invest.HandleErrorsWith(uiServices.NotificationService);
    }

    public object Icon => project.Banner;
    public object Picture => project.Picture;

    public ReactiveCommand<Unit, Result> Invest { get; }

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

    private Task<Maybe<Unit>> DoInvest(IProject project, UIServices uiServices)
    {
        uiServices.ActiveWallet.Current.Execute(w => investWizard.Invest(w, project));

        return uiServices.ActiveWallet.Current
            .Match(wallet => investWizard.Invest(wallet, project), () => ShowNoWalletMessage());
    }

    private async Task<Maybe<Unit>> ShowNoWalletMessage()
    {
        await uiServices.Dialog.ShowMessage("No wallet found", "Please create or recover a wallet to invest in this project.");
        return Maybe<Unit>.None;
    }
}