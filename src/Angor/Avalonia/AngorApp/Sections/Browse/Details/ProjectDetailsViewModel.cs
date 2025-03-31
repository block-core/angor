using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Angor.Wallet.Application;
using Angor.Wallet.Domain;
using AngorApp.Sections.Browse.Details.Invest.Amount;
using AngorApp.UI.Controls.Common.Success;
using AngorApp.UI.Controls.Common.TransactionDraft;
using AngorApp.UI.Services;
using Avalonia.Threading;
using Zafiro.Avalonia.Controls.Wizards.Builder;
using Zafiro.Avalonia.Dialogs;
using Zafiro.UI;

namespace AngorApp.Sections.Browse.Details;

public class ProjectDetailsViewModel : ReactiveObject, IProjectDetailsViewModel
{
    private readonly IProject project;

    public ProjectDetailsViewModel(IWalletAppService walletAppService, IProject project, UIServices uiServices)
    {
        this.project = project;
        Invest = ReactiveCommand.CreateFromTask(() =>
        {
            return walletAppService.GetMetadatas()
                .Map(x => x.TryFirst())
                .Bind(maybeMetadata =>
                {
                    if (maybeMetadata.HasValue)
                    {
                        return Result.Success(DoInvest(maybeMetadata.Value.Id, walletAppService, project, uiServices));
                    }
                    else
                    {
                        return Result.Success(Maybe<Unit>.None);
                    }
                });
        });

        Invest.HandleErrorsWith(uiServices.NotificationService);
    }

    public object Icon => project.Icon;
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

    private static async Task<Maybe<Unit>> DoInvest(WalletId walletId, IWalletAppService walletAppService, IProject project, UIServices uiServices)
    {
        return await Observable
            .Defer(() => Observable.FromAsync(() =>
            {
                var wizard = WizardBuilder.StartWith(() => new AmountViewModel(walletId, walletAppService, project))
                    .Then(viewModel =>
                    {
                        var destination = new Destination(project.Name, viewModel.Amount!.Value, project.BitcoinAddress);
                        return new TransactionDraftViewModel(walletId, walletAppService, destination, uiServices);
                    })
                    .Then(_ => new SuccessViewModel("Transaction confirmed!", "Success"))
                    .FinishWith(model => Unit.Default);
                
                return uiServices.Dialog.ShowWizard(wizard, @$"Invest in ""{project}""");
            }))
            .SubscribeOn(RxApp.MainThreadScheduler)
            .FirstAsync();
    }
}