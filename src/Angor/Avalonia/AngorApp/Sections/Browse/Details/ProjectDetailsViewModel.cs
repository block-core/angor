using System.Threading.Tasks;
using System.Windows.Input;
using AngorApp.Sections.Browse.Details.Invest.Amount;
using AngorApp.UI.Controls.Common.Success;
using AngorApp.UI.Controls.Common.TransactionDraft;
using AngorApp.UI.Services;
using Zafiro.Avalonia.Controls.Wizards.Builder;
using Zafiro.Avalonia.Dialogs;

namespace AngorApp.Sections.Browse.Details;

public class ProjectDetailsViewModel(IWalletProvider walletProvider, IProject project, UIServices uiServices)
    : ReactiveObject, IProjectDetailsViewModel
{
    public object Icon => project.Icon;
    public object Picture => project.Picture;

    public ICommand Invest { get; } = ReactiveCommand.CreateFromTask(() =>
    {
        var maybeWallet = walletProvider.GetWallet();
        return maybeWallet.Match(wallet => DoInvest(wallet, project, uiServices),
            () => uiServices.NotificationService.Show("You need to create a Wallet before investing", "No wallet"));
    });

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

    private static async Task<Maybe<Unit>> DoInvest(IWallet wallet, IProject project, UIServices uiServices)
    {
        var wizard = WizardBuilder.StartWith(() => new AmountViewModel(wallet, project))
            .Then(viewModel =>
            {
                var destination = new Destination(project.Name, viewModel.Amount!.Value, project.BitcoinAddress);
                return new TransactionDraftViewModel(wallet, destination, uiServices);
            })
            .Then(_ => new SuccessViewModel("Transaction confirmed!", "Success"))
            .FinishWith(model => Unit.Default);

        return await uiServices.Dialog.ShowWizard(wizard, @$"Invest in ""{project}""");
    }
}