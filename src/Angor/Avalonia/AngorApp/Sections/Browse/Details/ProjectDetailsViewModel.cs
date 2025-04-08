using System.Linq;
using System.Reactive.Threading.Tasks;
using System.Threading.Tasks;
using System.Windows.Input;
using Angor.Contexts.Wallet.Application;
using Angor.Contexts.Wallet.Domain;
using AngorApp.UI.Controls.Common.TransactionDraft;
using AngorApp.UI.Services;
using Avalonia.Threading;
using Zafiro.UI;

namespace AngorApp.Sections.Browse.Details;

public class ProjectDetailsViewModel : ReactiveObject, IProjectDetailsViewModel
{
    private readonly IProject project;
    private readonly InvestWizard investWizard;

    public ProjectDetailsViewModel(IWalletAppService walletAppService, IProject project, InvestWizard investWizard, UIServices uiServices)
    {
        this.project = project;
        this.investWizard = investWizard;
        Invest = ReactiveCommand.CreateFromTask(() =>
        {
            return walletAppService.GetMetadatas()
                .Map(x => x.TryFirst())
                .Bind(maybeMetadata =>
                {
                    if (maybeMetadata.HasValue)
                    {
                        return Result.Success(Observable.FromAsync(() => DoInvest(maybeMetadata.Value.Id, walletAppService, project, uiServices))
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

    private Task<Maybe<Unit>> DoInvest(WalletId walletId, IWalletAppService walletAppService, IProject project, UIServices uiServices)
    {
        return investWizard.Invest(walletId, project);
    }
}